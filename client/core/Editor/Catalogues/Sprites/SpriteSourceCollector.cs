using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Finds the marked art of the game and resolves its markup.
    internal static class SpriteSourceCollector
    {
        internal static List<SpriteSource> CollectSources()
        {
            var sources = new List<SpriteSource>();
            var artRoot = CatalogPaths.ToFullPath(FrameworkSettings.ArtFolder);

            if (Directory.Exists(artRoot) == false)
                return sources;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(artRoot, "*.*", SearchOption.AllDirectories);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                if (SpriteSourcePaths.IsSourceFile(file) == false)
                    continue;

                var path = CatalogPaths.ToAssetPath(projectRoot, file);

                if (SpriteSourcePaths.IsSourcePath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                var metadata = ResolveMetadata(importer);

                if (metadata == null)
                    continue;

                var groupName = CatalogNaming.ToGroupPath(metadata.Group);

                if (string.IsNullOrEmpty(groupName))
                {
                    Debug.LogError($"[SpriteGenerator] {path} is included and has no group: it is left out of the catalogue");
                    continue;
                }

                var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));

                if (string.IsNullOrEmpty(propertyName))
                {
                    Debug.LogError($"[SpriteGenerator] Identifier is empty for {path}");
                    continue;
                }

                sources.Add(new SpriteSource(
                    path,
                    groupName,
                    propertyName,
                    metadata.Kind == SpriteCatalogKind.Animation ? SpriteKind.Animation : SpriteKind.Sheet,
                    metadata.Kind == SpriteCatalogKind.MultipleAnimations && SpriteCatalogMetadata.IsAsepriteImporter(importer),
                    metadata,
                    metadata.Time,
                    metadata.Color,
                    GetPriority(path)));
            }

            return sources;
        }

        internal static SpriteCatalogMetadata ResolveMetadata(AssetImporter importer)
        {
            if (SpriteCatalogMetadata.TryRead(importer, out var metadata) == false)
                return null;

            if (metadata.Included == false)
                return null;

            if (string.IsNullOrEmpty(metadata.Group) == false)
                return metadata;

            metadata.SetGroup(SpriteCatalogMetadata.GetDefaultGroup(importer.assetPath));

            if (string.IsNullOrEmpty(metadata.Group))
            {
                Debug.LogError($"[SpriteGenerator] {importer.assetPath} is included and has no group: it is left out of the catalogue");
                return null;
            }

            SpriteCatalogMetadata.Write(importer, metadata);
            return metadata;
        }

        private static int GetPriority(string path)
        {
            var extension = Path.GetExtension(path);

            if (extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".ase", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (extension.Equals(".psd", StringComparison.OrdinalIgnoreCase))
                return 1;

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                return 2;

            return 3;
        }
    }
}
