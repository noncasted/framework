using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class AudioGroupAssetWriter
    {
        private const string LogTag = "AudioCatalogGenerator";

        internal static void WriteGroupAssets(IReadOnlyList<AudioGroupDefinition> groups, string groupsFolder)
        {
            CatalogPaths.EnsureFolder(groupsFolder);

            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var path = $"{groupsFolder}/{group.Name}.asset";
                writtenPaths.Add(path);
                WriteGroupAsset(path, group);
            }

            CatalogGroupAssets.DeleteStale<AudioGroupAsset>(groupsFolder, writtenPaths, LogTag);
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

                if (entry.FindPropertyRelative(AudioAssetEntry.NameKey).stringValue != property.PropertyName)
                    return false;

                if (entry.FindPropertyRelative(AudioAssetEntry.ClipKey).objectReferenceValue != property.Clip)
                    return false;

                if (entry.FindPropertyRelative(AudioAssetEntry.EncodedKey).objectReferenceValue != property.Encoded)
                    return false;

                if (Mathf.Approximately(entry.FindPropertyRelative(AudioAssetEntry.VolumeKey).floatValue, property.Volume) == false)
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
                entry.FindPropertyRelative(AudioAssetEntry.NameKey).stringValue = property.PropertyName;
                entry.FindPropertyRelative(AudioAssetEntry.ClipKey).objectReferenceValue = property.Clip;
                entry.FindPropertyRelative(AudioAssetEntry.EncodedKey).objectReferenceValue = property.Encoded;
                entry.FindPropertyRelative(AudioAssetEntry.VolumeKey).floatValue = property.Volume;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
