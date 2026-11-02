using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class AssetCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Assets");

        // Assets of non-Addressables groups: Resources.Load finds them by their path inside Resources.
        private const string ResourcesSubfolder = "AssetGroups";
        private static string ResourcesFolder => GroupsFolder + "/Resources/" + ResourcesSubfolder;

        private const string GroupPrefix = "Assets_";
        private const string LogTag = "AssetCatalogGenerator";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Assets Catalog", priority = FrameworkMenu.TrailingPriority)]
        public static void Generate()
        {
            Runner.Run();
        }

        public static void GenerateNow()
        {
            Runner.RunNow();
        }

        public static void ScheduleGenerate()
        {
            Runner.Schedule();
        }

        private static void GenerateInternal()
        {
            var groups = BuildGroups(CollectSources());

            // Moving between Addressables and Resources happens before batch editing: inside it
            // a moved asset cannot be read by its new path yet, and the group would be created again.
            MoveGroupAssets(groups);

            AssetDatabase.StartAssetEditing();

            try
            {
                WriteGroupAssets(groups);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Resources groups do not go into Addressables: sync would delete their addressable groups as extra.
            CatalogAddressablesSync.Sync(LogTag, GroupPrefix, GroupsFolder, groups.FindAll(group => group.IsAddressable));
            AssetsCatalogClassGenerator.Generate(groups);
            AssetGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} asset group(s).");
        }

        private static List<AssetSource> CollectSources()
        {
            var sources = new List<AssetSource>();
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                return sources;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                var path = CatalogPaths.ToAssetPath(projectRoot, file);

                if (string.IsNullOrEmpty(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                if (AssetCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);

                if (string.IsNullOrEmpty(groupName))
                {
                    Debug.LogError($"[{LogTag}] Included asset has empty group: {path}");
                    continue;
                }

                var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));

                if (string.IsNullOrEmpty(propertyName))
                {
                    Debug.LogError($"[{LogTag}] Identifier is empty for {path}");
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<EnvAsset>(path);

                if (asset == null)
                {
                    Debug.LogError($"[{LogTag}] {path} is marked for the catalog but is not an EnvAsset");
                    continue;
                }

                sources.Add(new AssetSource(path, groupName, propertyName, asset));
            }

            return sources;
        }

        internal static List<AssetGroupDefinition> BuildGroups(List<AssetSource> sources)
        {
            var groups = new Dictionary<string, AssetGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new AssetGroupDefinition(
                        source.Group,
                        source.Group + "Assets",
                        AssetGroupsRegistry.Instance.IsAddressable(source.Group),
                        $"{ResourcesSubfolder}/{source.Group}");
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                // Dropping one of the two would silently take an asset out of the catalogue.
                if (usedNames[source.Group].Add(source.PropertyName) == false)
                    throw new InvalidOperationException(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}': " +
                        $"{source.Path}. Rename one of the assets or move it to another group.");

                var type = source.Asset.GetType();
                var script = ScriptTypeNames.ScriptOf(source.Asset);

                group.AddProperty(new AssetPropertyDefinition(
                    source.PropertyName,
                    "_" + char.ToLowerInvariant(source.PropertyName[0]) + source.PropertyName.Substring(1),
                    source.Asset,
                    ScriptTypeNames.FullName(type, script).Replace('+', '.'),
                    ScriptTypeNames.AssemblyName(type, script)));
            }

            var result = new List<AssetGroupDefinition>();

            foreach (var group in groups.Values)
            {
                if (group.Properties.Count == 0)
                    continue;

                group.SortProperties();
                result.Add(group);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            AssignGeneratedTargets(result);
            return result;
        }

        // A group class must live in an assembly that sees the types of its assets,
        // so a group that mixes assemblies is not code-generated at all.
        internal static void AssignGeneratedTargets(List<AssetGroupDefinition> groups)
        {
            foreach (var group in groups)
            {
                var assemblies = new SortedSet<string>(StringComparer.Ordinal);

                foreach (var property in group.Properties)
                {
                    if (CatalogAssemblies.IsConsumerAssembly(property.TypeAssembly))
                        assemblies.Add(property.TypeAssembly);
                }

                if (assemblies.Count > 1)
                {
                    Debug.LogError(
                        $"[{LogTag}] Group '{group.Name}' mixes asset types from {string.Join(", ", assemblies)}. One group must stay in one assembly.");
                    group.DisableCodegen();
                    continue;
                }

                if (assemblies.Count == 1)
                {
                    group.SetGeneratedTarget(
                        CatalogAssemblies.ToNamespace(LogTag, assemblies.Min),
                        AssetsCatalogClassGenerator.GetAssemblyGeneratedFolder(assemblies.Min));
                    continue;
                }

                group.SetGeneratedTarget("Internal", AssetsCatalogClassGenerator.InternalGeneratedFolder);
            }
        }

        private static void WriteGroupAssets(IReadOnlyList<AssetGroupDefinition> groups)
        {
            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            CatalogGroupAssets.WriteAll(groups, GroupsFolder, static group => GetGroupAssetPath(group, group.IsAddressable), WriteGroupAsset, writtenPaths);

            // Resources lives inside GroupsFolder, so the search for stale assets covers it too.
            CatalogGroupAssets.DeleteStale<EnvAssetGroupAsset>(GroupsFolder, writtenPaths, LogTag);
        }

        private static string GetGroupAssetPath(AssetGroupDefinition group, bool isAddressable)
        {
            return isAddressable
                ? $"{GroupsFolder}/{group.Name}.asset"
                : $"{ResourcesFolder}/{group.Name}.asset";
        }

        private static void MoveGroupAssets(IReadOnlyList<AssetGroupDefinition> groups)
        {
            foreach (var group in groups)
            {
                CatalogGroupAssets.Move<EnvAssetGroupAsset>(
                    GetGroupAssetPath(group, group.IsAddressable == false),
                    GetGroupAssetPath(group, group.IsAddressable),
                    group.IsAddressable ? GroupsFolder : ResourcesFolder,
                    group.IsAddressable,
                    LogTag);
            }
        }

        private static void WriteGroupAsset(string path, AssetGroupDefinition group)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnvAssetGroupAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnvAssetGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var serialized = new SerializedObject(asset);
            var entries = serialized.FindProperty("_entries");

            if (EntriesMatch(entries, group.Properties) == false)
            {
                entries.arraySize = group.Properties.Count;

                for (var i = 0; i < group.Properties.Count; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative(EnvAssetEntry.NameKey).stringValue = group.Properties[i].PropertyName;
                    entry.FindPropertyRelative(EnvAssetEntry.AssetKey).objectReferenceValue = group.Properties[i].Asset;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            group.SetAddress(AssetDatabase.AssetPathToGUID(path));
        }

        private static bool EntriesMatch(SerializedProperty entries, IReadOnlyList<AssetPropertyDefinition> properties)
        {
            if (entries == null || entries.isArray == false || entries.arraySize != properties.Count)
                return false;

            for (var i = 0; i < properties.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative(EnvAssetEntry.NameKey).stringValue != properties[i].PropertyName)
                    return false;

                if (entry.FindPropertyRelative(EnvAssetEntry.AssetKey).objectReferenceValue != properties[i].Asset)
                    return false;
            }

            return true;
        }

        internal sealed class AssetSource
        {
            public AssetSource(string path, string group, string propertyName, EnvAsset asset)
            {
                Path = path;
                Group = group;
                PropertyName = propertyName;
                Asset = asset;
            }

            public string Path { get; }
            public string Group { get; }
            public string PropertyName { get; }
            public EnvAsset Asset { get; }
        }
    }

    internal sealed class AssetGroupDefinition : ICatalogGroupDefinition
    {
        private readonly List<AssetPropertyDefinition> _properties = new();

        public AssetGroupDefinition(string name, string className, bool isAddressable, string resourcePath)
        {
            Name = name;
            ClassName = className;
            IsAddressable = isAddressable;
            ResourcePath = resourcePath;
        }

        public string Name { get; }
        public string Address { get; private set; }
        public string ClassName { get; }
        public string TargetNamespace { get; private set; }
        public string GeneratedFolder { get; private set; }
        public bool SkipCodegen { get; private set; }
        public bool IsAddressable { get; }
        public string ResourcePath { get; }
        public IReadOnlyList<AssetPropertyDefinition> Properties => _properties;

        public void SetAddress(string address)
        {
            Address = address;
        }

        public void SetGeneratedTarget(string targetNamespace, string generatedFolder)
        {
            TargetNamespace = targetNamespace;
            GeneratedFolder = generatedFolder;
        }

        public void DisableCodegen()
        {
            SkipCodegen = true;
        }

        public void AddProperty(AssetPropertyDefinition property)
        {
            _properties.Add(property);
        }

        public void SortProperties()
        {
            _properties.Sort((left, right) =>
                string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
        }
    }

    internal sealed class AssetPropertyDefinition
    {
        public AssetPropertyDefinition(
            string propertyName,
            string fieldName,
            EnvAsset asset,
            string typeFullName,
            string typeAssembly)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
            Asset = asset;
            TypeFullName = typeFullName;
            TypeAssembly = typeAssembly;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public EnvAsset Asset { get; }
        public string TypeFullName { get; }
        public string TypeAssembly { get; }
    }
}