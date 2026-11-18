using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The files of the bundles of the catalog brought by the distribution of the platform: one fetch per file for every load
    // and prefetch that waits for it, the bytes a bundle and its dependencies fetch. Main thread only.
    internal sealed class ContentFileFetches
    {
        public ContentFileFetches(ContentCatalog catalog, IContentDistribution distribution)
        {
            _catalog = catalog;
            _distribution = distribution;
        }

        private readonly ContentCatalog _catalog;
        private readonly IContentDistribution _distribution;
        private readonly Dictionary<string, ContentFileFetch> _fetches = new();

        // The bytes the player fetches for the bundle and its dependencies: 0 when all are on the device by the build.
        public long Bytes(string name)
        {
            var bytes = 0L;

            foreach (var bundle in _catalog.Closure(name))
            {
                if (_distribution.IsFetched(bundle) == false)
                    continue;

                foreach (var file in bundle.Files)
                    bytes += file.Size;
            }

            return bytes;
        }

        // The files of the bundle and of its dependencies, nothing loaded (a prefetch, and the start of a hold).
        public async UniTask Fetch(IReadOnlyLifetime lifetime, string name, IProgress<long> progress)
        {
            var closure = _catalog.Closure(name);
            var fetched = 0L;
            var fetches = new List<UniTask>();

            foreach (var bundle in closure)
            {
                if (_distribution.IsFetched(bundle) == false)
                    continue;

                foreach (var file in bundle.Files)
                {
                    if (_distribution.IsReal(bundle, file))
                        fetched += file.Size;
                    else
                        fetches.Add(Counted(bundle, file));
                }
            }

            progress?.Report(fetched);
            await UniTask.WhenAll(fetches);

            async UniTask Counted(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
            {
                await FetchFile(lifetime, bundle, file);
                fetched += file.Size;
                progress?.Report(fetched);
            }
        }

        // The files of one bundle, before the engine loads them.
        public async UniTask FetchFiles(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle)
        {
            var fetches = new List<UniTask>(bundle.Files.Count);

            foreach (var file in bundle.Files)
                fetches.Add(FetchFile(lifetime, bundle, file));

            await UniTask.WhenAll(fetches);
        }

        private UniTask FetchFile(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            if (_distribution.IsReal(bundle, file))
                return UniTask.CompletedTask;

            if (_fetches.TryGetValue(file.Name, out var running) == false)
            {
                running = new ContentFileFetch(fetch => _distribution.Fetch(fetch, bundle, file), () => _fetches.Remove(file.Name));
                _fetches.Add(file.Name, running);
            }

            return running.Wait(lifetime);
        }
    }
}
