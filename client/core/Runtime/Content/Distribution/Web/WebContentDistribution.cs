// WebGL player only: outside it the native imports of WebContentFetch.jslib do not link (IL2CPP).
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // A WebGL player: a preloaded bundle is in the .data file (StreamingAssets/Content of the emscripten file system, as on
    // a disk); the others are fetched from <remote or StreamingAssets>/Content/<file>[.gz] into
    // temporaryCachePath/Content/<file> (WebContentFetch: the Cache API of the page keeps them, a .gz is unpacked by the
    // page) and deleted when their bundle is unloaded, so the memory of the page holds only the bundles in use.
    internal sealed class WebContentDistribution : IContentDistribution
    {
        private WebContentDistribution(string streamingAssets, string remote, string cache)
        {
            _local = $"{streamingAssets}/{ContentShippedPaths.Folder}";
            _remote = $"{remote ?? streamingAssets}/{ContentShippedPaths.Folder}";
            _cache = $"{cache}/{ContentShippedPaths.Folder}";
        }

        private readonly string _local;
        private readonly string _remote;
        private readonly string _cache;

        // The distribution of this player: at most MaxFetches requests to the server at once, and the cache of the page
        // keeps only the fetched files of this catalog (the ones of the builds before go).
        public static WebContentDistribution Open(ContentCatalog catalog, ContentOptions options)
        {
            WebContentFetch.Limit(options.MaxFetches);

            var distribution = new WebContentDistribution(Application.streamingAssetsPath, options.RemoteUrl, Application.temporaryCachePath);
            var urls = new List<string>();

            foreach (var bundle in catalog.Bundles)
            {
                if (bundle.IsPreloaded)
                    continue;

                foreach (var file in bundle.Files)
                    urls.Add(distribution.UrlOf(bundle, file));
            }

            WebContentFetch.KeepOnly(urls);
            return distribution;
        }

        public bool IsFetched(ContentCatalogBundleEntry bundle)
        {
            return bundle.IsPreloaded == false;
        }

        public bool IsReal(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return bundle.IsPreloaded || File.Exists(PathOf(bundle, file));
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            if (IsReal(bundle, file))
                return UniTask.CompletedTask;

            Directory.CreateDirectory(_cache);
            return WebContentFetch.File(lifetime, UrlOf(bundle, file), PathOf(bundle, file), bundle.IsGzip);
        }

        public string PathOf(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return $"{(bundle.IsPreloaded ? _local : _cache)}/{file.Name}";
        }

        public void Forget(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            if (bundle.IsPreloaded == false)
                File.Delete(PathOf(bundle, file));
        }

        // What the server holds: the file of the build, .gz for a bundle shipped in gzip.
        private string UrlOf(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return $"{_remote}/{ContentShippedPaths.Shipped(bundle, file)}";
        }
    }
}
#endif
