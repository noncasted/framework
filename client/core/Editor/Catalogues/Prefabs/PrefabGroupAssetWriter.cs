using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class PrefabGroupAssetWriter
    {
        internal static void WriteGroupAssets(IReadOnlyList<PrefabGroupDefinition> groups, string groupsFolder)
        {
            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            CatalogGroupAssets.WriteAll(groups, groupsFolder, group => $"{groupsFolder}/{group.Name}.asset", WriteGroupAsset, writtenPaths);

            // The search covers the subfolders: the assets a previous version kept in Resources are stale too.
            CatalogGroupAssets.DeleteStale<PrefabGroupAsset>(groupsFolder, writtenPaths);
        }

        private static void WriteGroupAsset(string path, PrefabGroupDefinition group)
        {
            var asset = AssetDatabase.LoadAssetAtPath<PrefabGroupAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PrefabGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            if (EntriesMatch(asset, group.Properties) == false)
            {
                ApplyEntries(asset, group.Properties);
                EditorUtility.SetDirty(asset);
            }

            group.SetGuid(AssetDatabase.AssetPathToGUID(path));
        }

        private static bool EntriesMatch(PrefabGroupAsset asset, IReadOnlyList<PrefabPropertyDefinition> properties)
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

                if (entry.FindPropertyRelative(PrefabAssetEntry.NameKey).stringValue != property.PropertyName)
                    return false;

                if (entry.FindPropertyRelative(PrefabAssetEntry.PrefabKey).objectReferenceValue != property.Prefab)
                    return false;

                if (entry.FindPropertyRelative(PrefabAssetEntry.ComponentTypeKey).stringValue != property.ComponentType)
                    return false;
            }

            return true;
        }

        private static void ApplyEntries(PrefabGroupAsset asset, IReadOnlyList<PrefabPropertyDefinition> properties)
        {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = properties.Count;

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(PrefabAssetEntry.NameKey).stringValue = property.PropertyName;
                entry.FindPropertyRelative(PrefabAssetEntry.PrefabKey).objectReferenceValue = property.Prefab;
                entry.FindPropertyRelative(PrefabAssetEntry.ComponentTypeKey).stringValue = property.ComponentType;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
