using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class AssetSourceCollector
    {
        private const string LogTag = "AssetCatalogGenerator";

        internal static List<AssetSource> CollectSources()
        {
            var sources = new List<AssetSource>();
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                return sources;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                var path = CatalogPaths.ToAssetPath(projectRoot, file);

                if (string.IsNullOrEmpty(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                if (AssetCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);

                if (string.IsNullOrEmpty(groupName))
                {
                    Debug.LogError($"[{LogTag}] Included asset has empty group: {path}");
                    continue;
                }

                var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));

                if (string.IsNullOrEmpty(propertyName))
                {
                    Debug.LogError($"[{LogTag}] Identifier is empty for {path}");
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<EnvAsset>(path);

                if (asset == null)
                {
                    Debug.LogError($"[{LogTag}] {path} is marked for the catalog but is not an EnvAsset");
                    continue;
                }

                sources.Add(new AssetSource(path, groupName, propertyName, asset));
            }

            return sources;
        }
    }
}
