using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Internal
{
    // The group assets of a catalog: written, moved between the Addressables folder and Resources, deleted when stale.
    internal static class CatalogGroupAssets
    {
        public static void WriteAll<TGroup>(
            IReadOnlyList<TGroup> groups,
            string groupsFolder,
            Func<TGroup, string> getPath,
            Action<string, TGroup> write,
            HashSet<string> writtenPaths)
        {
            CatalogPaths.EnsureFolder(groupsFolder);

            foreach (var group in groups)
            {
                var path = getPath(group);
                writtenPaths.Add(path);
                write(path, group);
            }
        }

        // The group asset is moved, not recreated: its GUID and references to it are preserved.
        public static void Move<TAsset>(string previousPath, string path, string folder, bool isAddressable, string logTag)
            where TAsset : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<TAsset>(previousPath) == null)
                return;

            CatalogPaths.EnsureFolder(folder);

            // An Addressables entry on an asset in Resources would produce a second copy in the bundle.
            if (isAddressable == false)
                AddressableAssetSettingsDefaultObject.Settings?.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(previousPath), false);

            var error = AssetDatabase.MoveAsset(previousPath, path);

            if (string.IsNullOrEmpty(error) == false)
                Debug.LogError($"[{logTag}] Failed to move {previousPath} to {path}: {error}");
        }

        public static void DeleteStale<TAsset>(string groupsFolder, HashSet<string> writtenPaths, string logTag)
            where TAsset : ScriptableObject
        {
            if (AssetDatabase.IsValidFolder(groupsFolder) == false)
                return;

            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}", new[] { groupsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (writtenPaths.Contains(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[{logTag}] Deleted stale group asset: {path}");
            }
        }
    }
}
