using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public sealed class AssetGroupsRegistry : CatalogGroupsRegistry
    {
        private AssetGroupsRegistry()
            : base("AssetGroupsRegistry", FrameworkSettings.Content("Runtime/Catalogues/Assets/AssetGroups.json"))
        {
        }

        public static AssetGroupsRegistry Instance { get; } = new();

        protected override IEnumerable<string> DiscoverGroups()
        {
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                yield break;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, "*.asset", SearchOption.AllDirectories);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                var assetPath = CatalogPaths.ToAssetPath(projectRoot, file);
                var importer = AssetImporter.GetAtPath(assetPath);

                if (importer == null)
                    continue;

                if (AssetCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included && string.IsNullOrEmpty(metadata.Group) == false && seen.Add(metadata.Group))
                    yield return metadata.Group;
            }
        }
    }
}