using System;
using System.Collections.Generic;
using System.IO;

namespace Internal
{
    // A root of the sprite sources: the folder itself and the prefix of the group names under it. The shared art folder
    // of the settings has no prefix; the Assets folder of a game module names its groups after the module
    // ("<Module>/<Folder>"), so a game that keeps runtime assets in the modules marks sprites there.
    internal readonly struct SpriteArtRoot
    {
        internal SpriteArtRoot(string folder, string groupPrefix, bool offersFolderGroups)
        {
            Folder = folder;
            GroupPrefix = groupPrefix;
            OffersFolderGroups = offersFolderGroups;
        }

        internal string Folder { get; }
        internal string GroupPrefix { get; }

        // The shared art folder holds only art: each of its folders is offered as a group. The Assets folder of a module
        // also holds textures, materials and UI art that never enter a catalogue: only the groups of marked sprites.
        internal bool OffersFolderGroups { get; }

        internal bool Contains(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) == false &&
                   assetPath.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase);
        }

        // The group of a source under this root, or empty when the source lies outside it.
        internal string GetGroup(string assetPath)
        {
            if (Contains(assetPath) == false)
                return string.Empty;

            var relative = assetPath.Substring(Folder.Length + 1);
            var directory = Path.GetDirectoryName(relative);
            var group = string.IsNullOrEmpty(directory) ? GroupPrefix : GroupPrefix + "/" + directory;
            return CatalogNaming.ToGroupPath(group);
        }
    }

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
