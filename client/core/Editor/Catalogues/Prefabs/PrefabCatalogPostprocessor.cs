using System;
using System.IO;
using UnityEditor;

namespace Internal
{
    public sealed class PrefabCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsPrefab(deletedAssets) ||
                ContainsPrefab(movedAssets) ||
                ContainsPrefab(movedFromAssetPaths) ||
                ContainsCatalogPrefab(importedAssets))
                PrefabCatalogGenerator.ScheduleGenerate();
        }

        private static bool ContainsPrefab(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                if (IsPrefabPath(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsCatalogPrefab(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];

                if (IsPrefabPath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                // Removal from the catalog also arrives as a reimport (Apply, a pulled .meta with included: false),
                // and the group must be rebuilt.
                if (PrefabCatalogMetadata.TryRead(importer, out _))
                    return true;
            }

            return false;
        }

        private static bool IsPrefabPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return Path.GetExtension(path).Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        }
    }
}