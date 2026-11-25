using System;
using System.Collections.Generic;
using System.IO;

namespace Internal
{
    // The folders a sprite catalogue reads: FrameworkSettings.ArtFolder and the Assets folder of every game module
    // (ModuleRoots). A root that lies inside another one is left out: the first root already names its sources.
    internal static class SpriteArtRoots
    {
        internal static IReadOnlyList<SpriteArtRoot> All
        {
            get
            {
                var roots = new List<SpriteArtRoot>();
                Add(roots, FrameworkSettings.ArtFolder, string.Empty, true);

                foreach (var moduleRoot in FrameworkSettings.ModuleRoots)
                {
                    var name = Path.GetFileName(moduleRoot.TrimEnd('/'));
                    Add(roots, moduleRoot.TrimEnd('/') + "/Assets", CatalogNaming.ToGroupName(name), false);
                }

                return roots;
            }
        }

        internal static bool HasExistingFolder
        {
            get
            {
                foreach (var root in All)
                {
                    if (Directory.Exists(CatalogPaths.ToFullPath(root.Folder)))
                        return true;
                }

                return false;
            }
        }

        internal static bool Contains(string assetPath)
        {
            return TryGet(assetPath, out _);
        }

        internal static bool TryGet(string assetPath, out SpriteArtRoot root)
        {
            foreach (var candidate in All)
            {
                if (candidate.Contains(assetPath))
                {
                    root = candidate;
                    return true;
                }
            }

            root = default;
            return false;
        }

        private static void Add(List<SpriteArtRoot> roots, string folder, string groupPrefix, bool offersFolderGroups)
        {
            if (string.IsNullOrEmpty(folder))
                return;

            foreach (var existing in roots)
            {
                if (IsInside(folder, existing.Folder) || IsInside(existing.Folder, folder))
                    return;
            }

            roots.Add(new SpriteArtRoot(folder, groupPrefix, offersFolderGroups));
        }

        private static bool IsInside(string folder, string root)
        {
            return string.Equals(folder, root, StringComparison.OrdinalIgnoreCase) ||
                   folder.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
