using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public sealed class SpriteGroupsRegistry : CatalogGroupsRegistry
    {
        private static readonly string[] SourceExtensions =
        {
            ".aseprite",
            ".ase",
            ".psd",
            ".png"
        };

        private SpriteGroupsRegistry()
            : base("SpriteGroupsRegistry", FrameworkSettings.Content("Runtime/Catalogues/Sprites/SpriteGroups.json"))
        {
        }

        public static SpriteGroupsRegistry Instance { get; } = new();

        // Sprite groups are nested: "Game.Players" gives Sprites.Game.Players.
        protected override string NormalizeGroup(string name)
        {
            return CatalogNaming.ToGroupPath(name);
        }

        protected override IEnumerable<string> DiscoverGroups()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var root in SpriteArtRoots.All)
            {
                var artRoot = CatalogPaths.ToFullPath(root.Folder);

                if (Directory.Exists(artRoot) == false)
                    continue;

                // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
                var files = Directory.GetFiles(artRoot, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (IsSourceFile(file) == false)
                        continue;

                    var assetPath = CatalogPaths.ToAssetPath(projectRoot, file);
                    var defaultGroup = SpriteCatalogMetadata.GetDefaultGroup(assetPath);

                    if (string.IsNullOrEmpty(defaultGroup) == false && seen.Add(defaultGroup))
                        yield return defaultGroup;

                    var importer = AssetImporter.GetAtPath(assetPath);

                    if (importer == null)
                        continue;

                    if (SpriteCatalogMetadata.TryRead(importer, out var metadata) == false)
                        continue;

                    if (metadata.Included && string.IsNullOrEmpty(metadata.Group) == false && seen.Add(metadata.Group))
                        yield return metadata.Group;
                }
            }
        }

        private static bool IsSourceFile(string path)
        {
            var extension = Path.GetExtension(path);

            foreach (var sourceExtension in SourceExtensions)
            {
                if (extension.Equals(sourceExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
