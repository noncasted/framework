using UnityEditor;

namespace Internal
{
    public sealed class SpriteCatalogPostprocessor : AssetPostprocessor
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
                SpriteGenerator.ScheduleGenerate();
        }

        private static bool ContainsSource(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                if (SpriteSourcePaths.IsSourcePath(paths[i]))
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

                if (SpriteSourcePaths.IsSourcePath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                // Removal from the catalog also arrives as a reimport on Apply, and the group must be rebuilt.
                if (SpriteCatalogMetadata.TryRead(importer, out _))
                    return true;
            }

            return false;
        }
    }
}