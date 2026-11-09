using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // A content directory fetched on demand into a local folder (the file system of the page): the registration needs the
    // manifest and the closure of the roots, every other file is written as zeros of its real size and fetched over them
    // before a load needs it. The engine lists the folder once and does not open the files of a lazy reference until it
    // is loaded, so the zeros are never read.
    internal sealed class FetchedContentStore : IContentStore
    {
        public FetchedContentStore(IContentFileSource source, string localPath)
        {
            LocalPath = localPath;
            _fetches = new ContentFileFetches(source, localPath);
        }

        private readonly ContentFileFetches _fetches;

        public string LocalPath { get; }

        public async UniTask Prepare(IReadOnlyLifetime lifetime, ContentIndex index)
        {
            // A folder of an earlier run would hold real files the fetches do not know.
            if (Directory.Exists(LocalPath))
                Directory.Delete(LocalPath, true);

            Directory.CreateDirectory(LocalPath);

            var registration = new List<ContentFile>(index.RootFiles.Count + 1) { index.Manifest };
            registration.AddRange(index.RootFiles);

            await _fetches.Fetch(lifetime, registration, null);

            File.WriteAllText(Path.Combine(LocalPath, ContentPaths.ManifestHashFile), index.ManifestHash);

            foreach (var file in index.Files)
            {
                if (_fetches.IsReal(file) == false)
                    WriteZeros(file);
            }
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

        private void WriteZeros(ContentFile file)
        {
            // SetLength fills with zeros without a buffer of the size in the managed heap.
            using var stream = new FileStream(Path.Combine(LocalPath, file.Name), FileMode.Create, FileAccess.Write);
            stream.SetLength(file.Size);
        }
    }
}
