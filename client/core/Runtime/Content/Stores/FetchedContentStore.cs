using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // A content directory fetched on demand into a local folder (the file system of the page): the archive is written as
    // zeros of its length, the registration needs its header, the manifest and the closure of the roots, and the spans of
    // the files of a load are fetched over the zeros before the load. The engine reads the header on the mount and the
    // blocks of a file only when it opens the file, so the zeros are never read.
    internal sealed class FetchedContentStore : IContentStore
    {
        // gap: ContentOptions.RangeGap.
        public FetchedContentStore(IContentRangeSource source, string localPath, long gap)
        {
            LocalPath = localPath;
            _source = source;
            _gap = gap;
        }

        private readonly IContentRangeSource _source;
        private readonly long _gap;
        private ContentFileFetches _fetches;

        public string LocalPath { get; }

        private string Archive => Path.Combine(LocalPath, ContentPaths.ArchiveFile);

        public async UniTask Prepare(IReadOnlyLifetime lifetime, ContentIndex index)
        {
            if (_fetches != null)
                throw new InvalidOperationException($"[Content] The store of {index.Name} at {LocalPath} is prepared already");

            // A folder of an earlier run would hold real bytes the fetches do not know.
            if (Directory.Exists(LocalPath))
                Directory.Delete(LocalPath, true);

            Directory.CreateDirectory(LocalPath);

            var files = new List<ContentFile>(index.Files.Count + 1) { index.Header };
            files.AddRange(index.Files);
            _fetches = new ContentFileFetches(_source, Archive, new ContentRanges(files, _gap));

            // SetLength fills with zeros without a buffer of the size in the managed heap.
            using (var stream = new FileStream(Archive, FileMode.Create, FileAccess.Write))
                stream.SetLength(index.ArchiveLength);

            File.WriteAllText(Path.Combine(LocalPath, ContentPaths.ManifestHashFile), index.ManifestHash);

            var registration = new List<ContentFile>(index.RootFiles.Count + 2) { index.Header, index.Manifest };
            registration.AddRange(index.RootFiles);

            await _fetches.Fetch(lifetime, registration, null);
            _source.Pin(Archive);
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, IReadOnlyList<ContentFile> files, IProgress<long> progress)
        {
            return _fetches.Fetch(lifetime, files, progress);
        }

        public bool IsReal(IReadOnlyList<ContentFile> files)
        {
            foreach (var file in files)
            {
                if (_fetches.IsReal(file) == false)
                    return false;
            }

            return true;
        }
    }
}
