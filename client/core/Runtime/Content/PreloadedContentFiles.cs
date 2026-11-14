using System;
using System.Collections.Generic;

namespace Internal
{
    // The files of a content directory preloaded into the .data file of a WebGL build: every file is real from the start,
    // so a load needs none fetched, and its progress has nothing to count (Total 0, as content on the disk of a player). The
    // full index (every file and object of the directory) is not shipped for it: its parse was a tenth of the start on a phone.
    internal sealed class PreloadedContentFiles : IContentFiles
    {
        public PreloadedContentFiles(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public IReadOnlyList<ContentFile> ObjectFiles(string guid, long localId)
        {
            return Array.Empty<ContentFile>();
        }

        public IReadOnlyList<ContentFile> SceneFiles(string guid)
        {
            return Array.Empty<ContentFile>();
        }
    }
}
