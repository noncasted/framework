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
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var root in SpriteArtRoots.All)
            {
                var rootFullPath = CatalogPaths.ToFullPath(root.Folder);

                if (Directory.Exists(rootFullPath) == false)
                    continue;

                // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
                var files = Directory.GetFiles(rootFullPath, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (SpriteSourcePaths.IsSourceFile(file) == false)
                        continue;

                    var path = CatalogPaths.ToAssetPath(projectRoot, file);

                    if (SpriteSourcePaths.IsSourcePath(path) == false)
                        continue;

                    if (seen.Add(path) == false)
                        continue;

                    var importer = CatalogPaths.SourceImporter(path);

                    if (importer == null)
                        continue;

                    var metadata = ResolveMetadata(importer);

                    if (metadata == null)
                        continue;

                    var groupName = CatalogNaming.ToGroupPath(metadata.Group);

                    if (string.IsNullOrEmpty(groupName))
                        throw new InvalidOperationException($"Included sprite has empty group: {path}");

                    var propertyName = CatalogNaming.ToGroupName(Path.GetFileNameWithoutExtension(path));

                    if (string.IsNullOrEmpty(propertyName))
                        throw new InvalidOperationException($"Identifier of the sprite is empty: {path}");

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
                throw new InvalidOperationException($"Included sprite has no group and no folder to take it from: {importer.assetPath}");

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
