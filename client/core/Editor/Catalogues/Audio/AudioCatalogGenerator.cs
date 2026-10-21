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
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Audio/Groups");

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

        [MenuItem("Tools/GenerateAudioCatalog")]
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

            string[] files;

            try
            {
                files = Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{logTag}] Failed to scan Assets: {exception}");
                yield break;
            }

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

                sources.Add(new ClipSource
                {
                    Path = path,
                    Group = groupName,
                    PropertyName = propertyName,
                    Variation = ParseVariation(fileName),
                    Clip = clip,
                    Encoded = encoded,
                    Volume = Mathf.Clamp01(metadata.Volume)
                });
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

        private static List<AudioGroupDefinition> BuildGroups(List<ClipSource> sources)
        {
            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            var groups = new Dictionary<string, AudioGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new AudioGroupDefinition
                    {
                        Name = source.Group,
                        ClassName = source.Group + "Audio"
                    };
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                if (usedNames[source.Group].Add(source.PropertyName) == false)
                {
                    Debug.LogError(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}'. Skipping {source.Path}.");
                    continue;
                }

                group.Properties.Add(new AudioPropertyDefinition
                {
                    PropertyName = source.PropertyName,
                    FieldName = ToFieldName(source.PropertyName),
                    Clip = source.Clip,
                    Encoded = source.Encoded,
                    Volume = source.Volume,
                    Variation = source.Variation
                });
            }

            var result = new List<AudioGroupDefinition>(groups.Values);

            foreach (var group in result)
            {
                group.Properties.Sort((left, right) => string.Compare(left.PropertyName, right.PropertyName,
                    StringComparison.Ordinal));
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

                    variation = new AudioVariationDefinition
                    {
                        PropertyName = key.Name,
                        FieldName = ToFieldName(key.Name)
                    };
                    variations.Add(key.Name, variation);
                    group.Variations.Add(variation);
                }

                variation?.Entries.Add(property);
            }

            foreach (var variation in group.Variations)
                variation.Entries.Sort((left, right) => left.Variation.Index.CompareTo(right.Variation.Index));

            group.Variations.Sort((left, right) => string.Compare(left.PropertyName, right.PropertyName,
                StringComparison.Ordinal));
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

            DeleteStaleAssets(writtenPaths);
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

            group.Address = AssetDatabase.AssetPathToGUID(path);
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

        private static void DeleteStaleAssets(HashSet<string> writtenPaths)
        {
            if (AssetDatabase.IsValidFolder(GroupsFolder) == false)
                return;

            var guids = AssetDatabase.FindAssets("t:AudioGroupAsset", new[] { GroupsFolder });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (writtenPaths.Contains(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[{LogTag}] Deleted stale group asset: {path}");
            }
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

        private sealed class ClipSource
        {
            public string Path;
            public string Group;
            public string PropertyName;
            public VariationKey Variation;
            public AudioClip Clip;
            public TextAsset Encoded;
            public float Volume;
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
        public string Name { get; set; }
        public string Address { get; set; }
        public string ClassName;
        public List<AudioPropertyDefinition> Properties = new();
        public List<AudioVariationDefinition> Variations = new();
    }

    internal sealed class AudioPropertyDefinition
    {
        public string PropertyName;
        public string FieldName;
        public VariationKey Variation;
        public AudioClip Clip;
        public TextAsset Encoded;
        public float Volume;
    }

    internal sealed class AudioVariationDefinition
    {
        public string PropertyName;
        public string FieldName;
        public List<AudioPropertyDefinition> Entries = new();
    }
}
