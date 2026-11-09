using System.Collections.Generic;

namespace Internal
{
    // A serialized file of a content directory manifest: the object it holds and the serialized files it depends on.
    public readonly struct ContentManifestFile
    {
        public ContentManifestFile(int index, string guid, ContentFileKind kind, long localId, string contentHash, IReadOnlyList<int> dependencies)
        {
            Index = index;
            Guid = guid;
            Kind = kind;
            LocalId = localId;
            ContentHash = contentHash;
            Dependencies = dependencies;
        }

        public int Index { get; }
        public string Guid { get; }
        public ContentFileKind Kind { get; }

        // The local id of the single object, 0 for a cluster (the whole asset).
        public long LocalId { get; }

        // Null for a file that comes with the player (the default resources).
        public string ContentHash { get; }

        public IReadOnlyList<int> Dependencies { get; }

        public bool IsInDirectory => ContentHash != null;
        public string FileName => $"{ContentHash}.cf";
    }
}
