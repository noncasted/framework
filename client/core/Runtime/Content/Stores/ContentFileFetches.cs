using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The files of one fetched content directory: which are real already and which are on their way. A file is fetched
    // once for all callers: two loads that need it wait for one download, and the download is aborted only when every
    // lifetime waiting for it has ended. Main thread only.
    internal sealed class ContentFileFetches
    {
        public ContentFileFetches(IContentFileSource source, string folder)
        {
            _source = source;
            _folder = folder;
        }

        private readonly IContentFileSource _source;
        private readonly string _folder;
        private readonly HashSet<string> _real = new();
        private readonly Dictionary<string, ContentFileFetch> _running = new();

        // The downloads going on now.
        public int Running => _running.Count;

        public bool IsReal(ContentFile file)
        {
            return _real.Contains(file.Name);
        }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, IReadOnlyList<ContentFile> files, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();

            var bytes = 0L;
            var waits = new List<UniTask>(files.Count);

            foreach (var file in files)
            {
                if (_real.Contains(file.Name))
                {
                    bytes += file.Size;
                    continue;
                }

                if (_running.TryGetValue(file.Name, out var fetch) == false)
                {
                    fetch = new ContentFileFetch(this, file);
                    _running.Add(file.Name, fetch);
                    fetch.Start(_source, Path.Combine(_folder, file.Name));
                }

                waits.Add(Wait(fetch));
            }

            progress?.Report(bytes);
            await UniTask.WhenAll(waits);

            async UniTask Wait(ContentFileFetch fetch)
            {
                await fetch.Wait(lifetime);
                bytes += fetch.File.Size;
                progress?.Report(bytes);
            }
        }

        internal void OnFetched(ContentFileFetch fetch)
        {
            _real.Add(fetch.File.Name);
            Forget(fetch);
        }

        // Failed or abandoned: the file is zeros, the next load fetches it again.
        internal void Forget(ContentFileFetch fetch)
        {
            if (_running.TryGetValue(fetch.File.Name, out var running) && running == fetch)
                _running.Remove(fetch.File.Name);
        }
    }
}
