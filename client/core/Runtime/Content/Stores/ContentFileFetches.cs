using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The files of the archive of one fetched content directory: which are real already and which are on their way.
    // The files a fetch needs that are neither go out as ranges (ContentRanges); a file is fetched once for all callers:
    // two loads that need it wait for one request, and a request is aborted only when every lifetime waiting for it has
    // ended. Main thread only.
    internal sealed class ContentFileFetches
    {
        public ContentFileFetches(IContentRangeSource source, string archive, ContentRanges ranges)
        {
            _source = source;
            _archive = archive;
            _ranges = ranges;
        }

        private readonly IContentRangeSource _source;
        private readonly string _archive;
        private readonly ContentRanges _ranges;
        private readonly HashSet<string> _real = new();
        private readonly Dictionary<string, ContentRangeFetch> _running = new();
        private readonly HashSet<ContentRangeFetch> _requests = new();

        // The requests going on now.
        public int Running => _requests.Count;

        public bool IsReal(ContentFile file)
        {
            return _real.Contains(file.Name);
        }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, IReadOnlyList<ContentFile> files, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();

            var bytes = 0L;
            var waits = new Dictionary<ContentRangeFetch, long>();
            var needed = new List<ContentFile>();
            var seen = new HashSet<string>();

            foreach (var file in files)
            {
                if (seen.Add(file.Name) == false)
                    continue;

                if (_real.Contains(file.Name))
                    bytes += file.Size;
                else if (_running.TryGetValue(file.Name, out var running))
                    Add(running, file);
                else
                    needed.Add(file);
            }

            needed.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            foreach (var range in _ranges.Merge(needed))
            {
                var fetch = new ContentRangeFetch(this, range);
                _requests.Add(fetch);

                foreach (var file in range.Files)
                {
                    if (_real.Contains(file.Name) == false && _running.ContainsKey(file.Name) == false)
                        _running.Add(file.Name, fetch);
                }

                fetch.Start(_source, _archive);
            }

            foreach (var file in needed)
                Add(_running[file.Name], file);

            progress?.Report(bytes);

            var tasks = new List<UniTask>(waits.Count);

            foreach (var pair in waits)
                tasks.Add(Wait(pair.Key, pair.Value));

            await UniTask.WhenAll(tasks);

            void Add(ContentRangeFetch fetch, ContentFile file)
            {
                waits.TryGetValue(fetch, out var sum);
                waits[fetch] = sum + file.Size;
            }

            async UniTask Wait(ContentRangeFetch fetch, long size)
            {
                await fetch.Wait(lifetime);
                bytes += size;
                progress?.Report(bytes);
            }
        }

        internal void OnFetched(ContentRangeFetch fetch)
        {
            foreach (var file in fetch.Range.Files)
                _real.Add(file.Name);

            Forget(fetch);
        }

        // Failed or abandoned: its files are zeros, the next load fetches them again.
        internal void Forget(ContentRangeFetch fetch)
        {
            _requests.Remove(fetch);

            foreach (var file in fetch.Range.Files)
            {
                if (_running.TryGetValue(file.Name, out var running) && running == fetch)
                    _running.Remove(file.Name);
            }
        }
    }
}
