using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class AssetGroupAssetWriter
    {
        private const string LogTag = "AssetCatalogGenerator";

        // Assets of non-Addressables groups: Resources.Load finds them by their path inside Resources.
        private static string ResourcesFolder(string groupsFolder)
        {
            return groupsFolder + "/Resources/" + AssetGroupDefinition.ResourcesSubfolder;
        }

        internal static void WriteGroupAssets(IReadOnlyList<AssetGroupDefinition> groups, string groupsFolder)
        {
            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            CatalogGroupAssets.WriteAll(groups, groupsFolder, group => GetGroupAssetPath(group, group.IsAddressable, groupsFolder), WriteGroupAsset, writtenPaths);

            // Resources lives inside groupsFolder, so the search for stale assets covers it too.
            CatalogGroupAssets.DeleteStale<EnvAssetGroupAsset>(groupsFolder, writtenPaths, LogTag);
        }

        private static string GetGroupAssetPath(AssetGroupDefinition group, bool isAddressable, string groupsFolder)
        {
            return isAddressable
                ? $"{groupsFolder}/{group.Name}.asset"
                : $"{ResourcesFolder(groupsFolder)}/{group.Name}.asset";
        }

        internal static void MoveGroupAssets(IReadOnlyList<AssetGroupDefinition> groups, string groupsFolder)
        {
            foreach (var group in groups)
            {
                CatalogGroupAssets.Move<EnvAssetGroupAsset>(
                    GetGroupAssetPath(group, group.IsAddressable == false, groupsFolder),
                    GetGroupAssetPath(group, group.IsAddressable, groupsFolder),
                    group.IsAddressable ? groupsFolder : ResourcesFolder(groupsFolder),
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
    }
}
