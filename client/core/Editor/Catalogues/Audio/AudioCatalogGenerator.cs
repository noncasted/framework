using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class AudioCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Audio");

        private const string GroupPrefix = "Audio_";
        private const string LogTag = "AudioCatalogGenerator";

        // Clips named Name_0, Name_1 are variations of one sound and are collected into a list.
        private static readonly Regex VariationPattern = new(@"^(.+?)_(\d+)$", RegexOptions.CultureInvariant);

        // Formats that Unity imports as AudioClip.
        private static readonly string[] SourceExtensions =
        {
            ".wav",
            ".mp3",
            ".ogg",
            ".aif",
            ".aiff",
            ".flac",
            ".mod",
            ".it",
            ".s3m",
            ".xm"
        };

        private static readonly CatalogGenerationRunner Runner = new(LogTag, GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Audio Catalog", priority = FrameworkMenu.TrailingPriority)]
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

        // Only the volume of already generated entries: group contents and code are not touched in Play mode.
        public static void SyncVolumes(IReadOnlyList<AssetImporter> importers)
        {
            foreach (var importer in importers)
            {
                if (AudioCatalogMetadata.TryRead(importer, out var metadata) == false || metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);
                var asset = AssetDatabase.LoadAssetAtPath<AudioGroupAsset>($"{GroupsFolder}/{groupName}.asset");

                if (asset == null)
                    continue;

                var name = CatalogNaming.ToGroupName(GetSourceName(importer.assetPath));

                if (asset.TrySetVolume(name, Mathf.Clamp01(metadata.Volume)))
                    EditorUtility.SetDirty(asset);
            }
        }

        public static bool IsSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (IsEncodedPath(path))
                return true;

            var extension = Path.GetExtension(path);

            foreach (var sourceExtension in SourceExtensions)
            {
                if (extension.Equals(sourceExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // A clip shipped as ready mp3 bytes (EncodedAudio): Unity does not re-encode it to AAC for WebGL.
        public static bool IsEncodedPath(string path)
        {
            return path.EndsWith(EncodedAudio.Extension, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSourceName(string path)
        {
            var fileName = Path.GetFileName(path);

            return IsEncodedPath(path)
                ? fileName.Substring(0, fileName.Length - EncodedAudio.Extension.Length)
                : Path.GetFileNameWithoutExtension(fileName);
        }

        // Tagged clips are needed by both the generator and the group registry, so the traversal is shared.
        internal static IEnumerable<MarkedSource> EnumerateMarked(string logTag)
        {
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                yield break;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                if (IsSourcePath(file) == false)
                    continue;

                var path = CatalogPaths.ToAssetPath(projectRoot, file);
                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                if (AudioCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                yield return new MarkedSource(path, metadata);
            }
        }

        private static void GenerateInternal()
        {
            var groups = BuildGroups(CollectSources());

            AssetDatabase.StartAssetEditing();

            try
            {
                WriteGroupAssets(groups);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CatalogAddressablesSync.Sync(LogTag, GroupPrefix, GroupsFolder, groups);
            AudioCatalogClassGenerator.Generate(groups);
            AudioGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} audio group(s).");
        }

        private static List<ClipSource> CollectSources()
        {
            var sources = new List<ClipSource>();

            foreach (var marked in EnumerateMarked(LogTag))
            {
                var path = marked.Path;
                var metadata = marked.Metadata;

                if (metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);

                if (string.IsNullOrEmpty(groupName))
                {
                    Debug.LogError($"[{LogTag}] Included audio clip has empty group: {path}");
                    continue;
                }

                var fileName = GetSourceName(path);
                var propertyName = CatalogNaming.ToGroupName(fileName);

                if (string.IsNullOrEmpty(propertyName))
                {
                    Debug.LogError($"[{LogTag}] Identifier is empty for {path}");
                    continue;
                }

                var isEncoded = IsEncodedPath(path);
                var clip = isEncoded ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var encoded = isEncoded ? AssetDatabase.LoadAssetAtPath<TextAsset>(path) : null;

                if (clip == null && encoded == null)
                {
                    Debug.LogError($"[{LogTag}] Failed to load audio clip at {path}");
                    continue;
                }

                sources.Add(new ClipSource(
                    path,
                    groupName,
                    propertyName,
                    ParseVariation(fileName),
                    clip,
                    encoded,
                    Mathf.Clamp01(metadata.Volume)));
            }

            return sources;
        }

        private static VariationKey ParseVariation(string fileName)
        {
            var match = VariationPattern.Match(fileName);

            if (match.Success == false)
                return null;

            var name = CatalogNaming.ToGroupName(match.Groups[1].Value);

            if (string.IsNullOrEmpty(name) || int.TryParse(match.Groups[2].Value, out var index) == false)
                return null;

            return new VariationKey(name, index);
        }

        internal static List<AudioGroupDefinition> BuildGroups(List<ClipSource> sources)
        {
            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            var groups = new Dictionary<string, AudioGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new AudioGroupDefinition(source.Group, source.Group + "Audio");
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                // Dropping one of the two would silently take a sound out of the catalogue.
                if (usedNames[source.Group].Add(source.PropertyName) == false)
                    throw new InvalidOperationException(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}': " +
                        $"{source.Path}. Rename one of the clips or move it to another group.");

                group.AddProperty(new AudioPropertyDefinition(
                    source.PropertyName,
                    ToFieldName(source.PropertyName),
                    source.Variation,
                    source.Clip,
                    source.Encoded,
                    source.Volume));
            }

            var result = new List<AudioGroupDefinition>(groups.Values);

            foreach (var group in result)
            {
                group.SortProperties();
                BuildVariations(group, usedNames[group.Name]);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        private static void BuildVariations(AudioGroupDefinition group, HashSet<string> usedNames)
        {
            var variations = new Dictionary<string, AudioVariationDefinition>(StringComparer.Ordinal);

            foreach (var property in group.Properties)
            {
                var key = property.Variation;

                if (key == null)
                    continue;

                if (variations.TryGetValue(key.Name, out var variation) == false)
                {
                    if (usedNames.Contains(key.Name))
                    {
                        Debug.LogError(
                            $"[{LogTag}] Variation list '{key.Name}' in group '{group.Name}' clashes with a single clip. Skipping variations.");
                        variations.Add(key.Name, null);
                        continue;
                    }

                    variation = new AudioVariationDefinition(key.Name, ToFieldName(key.Name));
                    variations.Add(key.Name, variation);
                    group.AddVariation(variation);
                }

                variation?.AddEntry(property);
            }

            foreach (var variation in group.Variations)
                variation.SortEntries();

            group.SortVariations();
        }

        private static string ToFieldName(string propertyName)
        {
            return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }

        private static void WriteGroupAssets(IReadOnlyList<AudioGroupDefinition> groups)
        {
            CatalogPaths.EnsureFolder(GroupsFolder);

            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var path = $"{GroupsFolder}/{group.Name}.asset";
                writtenPaths.Add(path);
                WriteGroupAsset(path, group);
            }

            CatalogGroupAssets.DeleteStale<AudioGroupAsset>(GroupsFolder, writtenPaths, LogTag);
        }

        private static void WriteGroupAsset(string path, AudioGroupDefinition group)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AudioGroupAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AudioGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            if (EntriesMatch(asset, group.Properties) == false)
            {
                ApplyEntries(asset, group.Properties);
                EditorUtility.SetDirty(asset);
            }

            group.SetAddress(AssetDatabase.AssetPathToGUID(path));
        }

        private static bool EntriesMatch(AudioGroupAsset asset, IReadOnlyList<AudioPropertyDefinition> properties)
        {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");

            if (entries == null || entries.isArray == false)
                return false;

            if (entries.arraySize != properties.Count)
                return false;

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative("Name").stringValue != property.PropertyName)
                    return false;

                if (entry.FindPropertyRelative("Clip").objectReferenceValue != property.Clip)
                    return false;

                if (entry.FindPropertyRelative("Encoded").objectReferenceValue != property.Encoded)
                    return false;

                if (Mathf.Approximately(entry.FindPropertyRelative("Volume").floatValue, property.Volume) == false)
                    return false;
            }

            return true;
        }

        private static void ApplyEntries(AudioGroupAsset asset, IReadOnlyList<AudioPropertyDefinition> properties)
        {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = properties.Count;

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Name").stringValue = property.PropertyName;
                entry.FindPropertyRelative("Clip").objectReferenceValue = property.Clip;
                entry.FindPropertyRelative("Encoded").objectReferenceValue = property.Encoded;
                entry.FindPropertyRelative("Volume").floatValue = property.Volume;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }


        internal readonly struct MarkedSource
        {
            public MarkedSource(string path, AudioCatalogMetadata metadata)
            {
                Path = path;
                Metadata = metadata;
            }

            public string Path { get; }
            public AudioCatalogMetadata Metadata { get; }
        }

        internal sealed class ClipSource
        {
            public ClipSource(
                string path,
                string group,
                string propertyName,
                VariationKey variation,
                AudioClip clip,
                TextAsset encoded,
                float volume)
            {
                Path = path;
                Group = group;
                PropertyName = propertyName;
                Variation = variation;
                Clip = clip;
                Encoded = encoded;
                Volume = volume;
            }

            public string Path { get; }
            public string Group { get; }
            public string PropertyName { get; }
            public VariationKey Variation { get; }
            public AudioClip Clip { get; }
            public TextAsset Encoded { get; }
            public float Volume { get; }
        }
    }

    internal sealed class VariationKey
    {
        public VariationKey(string name, int index)
        {
            Name = name;
            Index = index;
        }

        public string Name { get; }
        public int Index { get; }
    }

    internal sealed class AudioGroupDefinition : ICatalogGroupDefinition
    {
        private readonly List<AudioPropertyDefinition> _properties = new();
        private readonly List<AudioVariationDefinition> _variations = new();

        public AudioGroupDefinition(string name, string className)
        {
            Name = name;
            ClassName = className;
        }

        public string Name { get; }
        public string Address { get; private set; }
        public string ClassName { get; }
        public IReadOnlyList<AudioPropertyDefinition> Properties => _properties;
        public IReadOnlyList<AudioVariationDefinition> Variations => _variations;

        public void SetAddress(string address)
        {
            Address = address;
        }

        public void AddProperty(AudioPropertyDefinition property)
        {
            _properties.Add(property);
        }

        public void SortProperties()
        {
            _properties.Sort((left, right) =>
                string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
        }

        public void AddVariation(AudioVariationDefinition variation)
        {
            _variations.Add(variation);
        }

        public void SortVariations()
        {
            _variations.Sort((left, right) =>
                string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
        }
    }

    internal sealed class AudioPropertyDefinition
    {
        public AudioPropertyDefinition(
            string propertyName,
            string fieldName,
            VariationKey variation,
            AudioClip clip,
            TextAsset encoded,
            float volume)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
            Variation = variation;
            Clip = clip;
            Encoded = encoded;
            Volume = volume;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public VariationKey Variation { get; }
        public AudioClip Clip { get; }
        public TextAsset Encoded { get; }
        public float Volume { get; }
    }

    internal sealed class AudioVariationDefinition
    {
        private readonly List<AudioPropertyDefinition> _entries = new();

        public AudioVariationDefinition(string propertyName, string fieldName)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public IReadOnlyList<AudioPropertyDefinition> Entries => _entries;

        public void AddEntry(AudioPropertyDefinition entry)
        {
            _entries.Add(entry);
        }

        public void SortEntries()
        {
            _entries.Sort((left, right) => left.Variation.Index.CompareTo(right.Variation.Index));
        }
    }
}
