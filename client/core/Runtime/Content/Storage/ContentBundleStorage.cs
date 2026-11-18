using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The bundles of the catalog held by lifetimes: a hold of a bundle holds its dependencies too (the catalog lists them
    // all), each counted apart; their files come through the fetches. Main thread only.
    internal sealed class ContentBundleStorage
    {
        public ContentBundleStorage(ContentCatalog catalog, IContentDistribution distribution, ContentFileFetches fetches)
        {
            Catalog = catalog;
            _distribution = distribution;
            _fetches = fetches;

            foreach (var bundle in catalog.Bundles)
                _stored.Add(bundle.Name, new ContentStoredBundle(bundle, distribution, fetches));
        }

        private readonly IContentDistribution _distribution;
        private readonly ContentFileFetches _fetches;
        private readonly Dictionary<string, ContentStoredBundle> _stored = new(StringComparer.OrdinalIgnoreCase);

        public ContentCatalog Catalog { get; }

        // The bundle and its dependencies load synchronously: loaded, or every file on the device.
        public bool IsReal(string name)
        {
            foreach (var bundle in Catalog.Closure(name))
            {
                if (_stored[bundle.Name].IsLoaded)
                    continue;

                foreach (var file in bundle.Files)
                {
                    if (_distribution.IsReal(bundle, file) == false)
                        return false;
                }
            }

            return true;
        }

        // The bundle and its dependencies loaded while the hold lives.
        public async UniTask Hold(IReadOnlyLifetime hold, string name, IProgress<long> progress)
        {
            await _fetches.Fetch(hold, name, progress);

            var closure = Catalog.Closure(name);
            var loads = new UniTask[closure.Count];

            for (var i = 0; i < closure.Count; i++)
                loads[i] = _stored[closure[i].Name].Hold(hold);

            await UniTask.WhenAll(loads);
        }

        // Synchronous: throws when a file of the bundle or of a dependency needs a fetch or a load of one runs.
        public void HoldNow(IReadOnlyLifetime hold, string name)
        {
            if (IsReal(name) == false)
                throw new InvalidOperationException($"[Content] The content bundle {name} needs a fetch: load it with Load");

            foreach (var bundle in Catalog.Closure(name))
                _stored[bundle.Name].HoldNow(hold);
        }

        public ContentStoredBundle Bundle(string name)
        {
            Catalog.Bundle(name);
            return _stored[name];
        }
    }
}
