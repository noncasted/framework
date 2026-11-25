using System;
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
}
