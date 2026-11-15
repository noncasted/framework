using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class PrefabSourceCollector
    {
        internal static List<PrefabSource> CollectSources()
        {
            var sources = new List<PrefabSource>();
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                return sources;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                var path = CatalogPaths.ToAssetPath(projectRoot, file);

                if (string.IsNullOrEmpty(path))
                    continue;

                var importer = CatalogPaths.SourceImporter(path);

                if (importer == null)
                    continue;

                if (PrefabCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);

                if (string.IsNullOrEmpty(groupName))
                    throw new InvalidOperationException($"Included prefab has empty group: {path}");

                var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));

                if (string.IsNullOrEmpty(propertyName))
                    throw new InvalidOperationException($"Identifier of the prefab is empty: {path}");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    throw new InvalidOperationException($"Failed to load the prefab of the catalogue: {path}");

                sources.Add(new PrefabSource(
                    path,
                    groupName,
                    propertyName,
                    prefab,
                    PrefabRootResolver.ResolveComponentType(path, prefab, metadata)));
            }

            return sources;
        }
    }
}
