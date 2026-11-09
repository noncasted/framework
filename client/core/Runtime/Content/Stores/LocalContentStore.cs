using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // A content directory whose files are all on the disk already: StreamingAssets of a standalone player, a directory
    // preloaded into the .data file of a WebGL build. Nothing to fetch.
    internal sealed class LocalContentStore : IContentStore
    {
        public LocalContentStore(string localPath)
        {
            LocalPath = localPath;
        }

        public string LocalPath { get; }

        public UniTask Prepare(IReadOnlyLifetime lifetime, ContentIndex index)
        {
            // No check of the folder here: on WebGL a preloaded directory is read through the URL of StreamingAssets,
            // which System.IO does not see; a missing folder fails the registration.
            lifetime.Token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, IReadOnlyList<ContentFile> files, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();

            if (progress != null)
            {
                var bytes = 0L;

                foreach (var file in files)
                    bytes += file.Size;

                progress.Report(bytes);
            }

            return UniTask.CompletedTask;
        }
    }
}
