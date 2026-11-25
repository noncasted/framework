using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The group assets of a catalog: written, deleted when stale.
    internal static class CatalogGroupAssets
    {
        private static readonly ILog _log = Logs.Of(typeof(CatalogGroupAssets));

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

        public static void DeleteStale<TAsset>(string groupsFolder, HashSet<string> writtenPaths)
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
                _log.Info("Deleted stale group asset: {Path}", path);
            }
        }
    }
}
