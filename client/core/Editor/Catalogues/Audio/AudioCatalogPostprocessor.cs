using UnityEditor;

namespace Internal
{
    public sealed class AudioCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsSource(deletedAssets) ||
                ContainsSource(movedAssets) ||
                ContainsSource(movedFromAssetPaths) ||
                ContainsCatalogSource(importedAssets))
                AudioCatalogGenerator.ScheduleGenerate();
        }

        private static bool ContainsSource(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                if (AudioCatalogGenerator.IsSourcePath(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsCatalogSource(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];

                if (AudioCatalogGenerator.IsSourcePath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                // Removal from the catalog also arrives as a reimport (Apply, a pulled .meta with included: false),
                // and the group must be rebuilt.
                if (AudioCatalogMetadata.TryRead(importer, out _))
                    return true;
            }

            return false;
        }
    }
}
