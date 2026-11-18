using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The bundles of the catalog over the store of the platform: a hold of a bundle holds its dependencies too (the
    // catalog lists them all), each counted apart; one fetch per file for every load and prefetch that waits for it.
    // Main thread only.
    internal sealed class ContentBundleSet
    {
        public ContentBundleSet(ContentCatalog catalog, IContentBundleStore store)
        {
            Catalog = catalog;
            _store = store;

            foreach (var bundle in catalog.Bundles)
                _states.Add(bundle.Name, new ContentBundleState(bundle, store, FetchFiles));
        }

        private readonly IContentBundleStore _store;
        private readonly Dictionary<string, ContentBundleState> _states = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ContentSharedFetch> _fetches = new();

        public ContentCatalog Catalog { get; }

        // The bytes the player fetches for the bundle and its dependencies: 0 when all are on the device by the build.
        public long Bytes(string name)
        {
            var bytes = 0L;

            foreach (var bundle in Closure(name))
            {
                if (_store.IsFetched(bundle) == false)
                    continue;

                foreach (var file in bundle.Files)
                    bytes += file.Size;
            }

            return bytes;
        }

        // The bundle and its dependencies load synchronously: loaded, or every file on the device.
        public bool IsReal(string name)
        {
            foreach (var bundle in Closure(name))
            {
                if (_states[bundle.Name].IsLoaded)
                    continue;

                foreach (var file in bundle.Files)
                {
                    if (_store.IsReal(bundle, file) == false)
                        return false;
                }
            }

            return true;
        }

        // The files of the bundle and of its dependencies, nothing loaded (a prefetch).
        public async UniTask Fetch(IReadOnlyLifetime lifetime, string name, IProgress<long> progress)
        {
            var closure = Closure(name);
            var fetched = 0L;
            var fetches = new List<UniTask>();

            foreach (var bundle in closure)
            {
                if (_store.IsFetched(bundle) == false)
                    continue;

                foreach (var file in bundle.Files)
                {
                    if (_store.IsReal(bundle, file))
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

        // The bundle and its dependencies loaded while the hold lives.
        public async UniTask Hold(IReadOnlyLifetime hold, string name, IProgress<long> progress)
        {
            await Fetch(hold, name, progress);

            var closure = Closure(name);
            var loads = new UniTask[closure.Count];

            for (var i = 0; i < closure.Count; i++)
                loads[i] = _states[closure[i].Name].Hold(hold);

            await UniTask.WhenAll(loads);
        }

        // Synchronous: throws when a file of the bundle or of a dependency needs a fetch or a load of one runs.
        public void HoldNow(IReadOnlyLifetime hold, string name)
        {
            if (IsReal(name) == false)
                throw new InvalidOperationException($"[Content] The content bundle {name} needs a fetch: load it with Load");

            foreach (var bundle in Closure(name))
                _states[bundle.Name].HoldNow(hold);
        }

        public ContentBundleState State(string name)
        {
            Catalog.Bundle(name);
            return _states[name];
        }

        // The dependencies first, then the bundle.
        private IReadOnlyList<ContentCatalogBundleEntry> Closure(string name)
        {
            var bundle = Catalog.Bundle(name);
            var closure = new List<ContentCatalogBundleEntry>(bundle.Dependencies.Count + 1);

            foreach (var dependency in bundle.Dependencies)
                closure.Add(Catalog.Bundle(dependency));

            closure.Add(bundle);
            return closure;
        }

        private async UniTask FetchFiles(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle)
        {
            var fetches = new List<UniTask>(bundle.Files.Count);

            foreach (var file in bundle.Files)
                fetches.Add(FetchFile(lifetime, bundle, file));

            await UniTask.WhenAll(fetches);
        }

        private UniTask FetchFile(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            if (_store.IsReal(bundle, file))
                return UniTask.CompletedTask;

            if (_fetches.TryGetValue(file.Name, out var running) == false)
            {
                running = new ContentSharedFetch(fetch => _store.Fetch(fetch, bundle, file), () => _fetches.Remove(file.Name));
                _fetches.Add(file.Name, running);
            }

            return running.Wait(lifetime);
        }
    }
}
