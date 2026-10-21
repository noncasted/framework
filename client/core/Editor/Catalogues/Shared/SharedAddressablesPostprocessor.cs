using System;
using System.IO;
using UnityEditor;

namespace Internal
{
    // References between assets change in scenes, prefabs and materials. Catalog assets
    // are not included here: their generators schedule the recalculation themselves after sync.
    public sealed class SharedAddressablesPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsReferencingAsset(importedAssets) ||
                ContainsReferencingAsset(deletedAssets) ||
                ContainsReferencingAsset(movedAssets))
                SharedAddressablesSync.ScheduleSync();
        }

        private static bool ContainsReferencingAsset(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                var extension = Path.GetExtension(paths[i]);

                if (extension.Equals(".unity", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".mat", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}