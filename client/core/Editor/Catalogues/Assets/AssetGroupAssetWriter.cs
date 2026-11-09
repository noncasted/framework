using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class AssetGroupAssetWriter
    {
        private const string LogTag = "AssetCatalogGenerator";

        internal static void WriteGroupAssets(IReadOnlyList<AssetGroupDefinition> groups, string groupsFolder)
        {
            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            CatalogGroupAssets.WriteAll(groups, groupsFolder, group => $"{groupsFolder}/{group.Name}.asset", WriteGroupAsset, writtenPaths);

            // The search covers the subfolders: the assets a previous version kept in Resources are stale too.
            CatalogGroupAssets.DeleteStale<EnvAssetGroupAsset>(groupsFolder, writtenPaths, LogTag);
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

            group.SetGuid(AssetDatabase.AssetPathToGUID(path));
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
    }
}
