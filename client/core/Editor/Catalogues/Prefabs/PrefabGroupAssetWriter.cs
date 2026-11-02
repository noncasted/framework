using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class PrefabGroupAssetWriter
    {
        private const string LogTag = "PrefabCatalogGenerator";

        // Assets of non-Addressables groups: Resources.Load finds them by their path inside Resources.
        private static string ResourcesFolder(string groupsFolder)
        {
            return groupsFolder + "/Resources/" + PrefabGroupDefinition.ResourcesSubfolder;
        }

        internal static void WriteGroupAssets(IReadOnlyList<PrefabGroupDefinition> groups, string groupsFolder)
        {
            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            CatalogGroupAssets.WriteAll(groups, groupsFolder, group => GetGroupAssetPath(group, group.IsAddressable, groupsFolder), WriteGroupAsset, writtenPaths);

            // Resources lives inside groupsFolder, so the search for stale assets covers it too.
            CatalogGroupAssets.DeleteStale<PrefabGroupAsset>(groupsFolder, writtenPaths, LogTag);
        }

        private static string GetGroupAssetPath(PrefabGroupDefinition group, bool isAddressable, string groupsFolder)
        {
            return isAddressable
                ? $"{groupsFolder}/{group.Name}.asset"
                : $"{ResourcesFolder(groupsFolder)}/{group.Name}.asset";
        }

        internal static void MoveGroupAssets(IReadOnlyList<PrefabGroupDefinition> groups, string groupsFolder)
        {
            foreach (var group in groups)
            {
                CatalogGroupAssets.Move<PrefabGroupAsset>(
                    GetGroupAssetPath(group, group.IsAddressable == false, groupsFolder),
                    GetGroupAssetPath(group, group.IsAddressable, groupsFolder),
                    group.IsAddressable ? groupsFolder : ResourcesFolder(groupsFolder),
                    group.IsAddressable,
                    LogTag);
            }
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

            group.SetAddress(AssetDatabase.AssetPathToGUID(path));
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
