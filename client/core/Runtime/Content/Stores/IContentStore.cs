using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // Where a content directory lives on this platform: the folder the engine registers and reads, and how its files
    // get there. A variant per platform: the files on the disk of the player (LocalContentStore) or fetched on demand
    // into the file system of the page (FetchedContentStore over WebContentFetch).
    internal interface IContentStore
    {
        // The folder ContentLoadManager.RegisterContentDirectory reads.
        string LocalPath { get; }

        // Makes the folder registrable: the manifest, BuildManifestHash.txt and the root files are real, every other file
        // of the index exists at its size (zeros until it is fetched). The engine lists the folder once, at the registration.
        UniTask Prepare(IReadOnlyLifetime lifetime, ContentIndex index);

        // Makes the files real, each fetched once for all callers. Progress: the bytes of the files real so far.
        // Throws OperationCanceledException when the lifetime ends first; a file nobody else waits for stays zeros.
        UniTask Fetch(IReadOnlyLifetime lifetime, IReadOnlyList<ContentFile> files, IProgress<long> progress);

        // Every one of the files is real, none is zeros.
        bool IsReal(IReadOnlyList<ContentFile> files);
    }
}
