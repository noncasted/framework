// WebGL player only: outside it the native imports of WebContentFetch.jslib do not link (IL2CPP).
#if UNITY_WEBGL && !UNITY_EDITOR
using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // A WebGL player: a preloaded bundle is in the .data file (StreamingAssets/Content of the emscripten file system, as on
    // a disk); the others are fetched from <remote or StreamingAssets>/Content/<file>[.gz] into
    // temporaryCachePath/Content/<file> (WebContentFetch: the Cache API of the page keeps them, a .gz is unpacked by the
    // page) and deleted when their bundle is unloaded, so the memory of the page holds only the bundles in use.
    internal sealed class WebContentBundleStore : IContentBundleStore
    {
        public WebContentBundleStore(string streamingAssets, string remote, string cache)
        {
            _local = $"{streamingAssets}/{ContentCatalogPaths.Folder}";
            _remote = $"{remote ?? streamingAssets}/{ContentCatalogPaths.Folder}";
            _cache = $"{cache}/{ContentCatalogPaths.Folder}";
        }

        private readonly string _local;
        private readonly string _remote;
        private readonly string _cache;

        public bool IsFetched(ContentCatalogBundle bundle)
        {
            return bundle.IsPreloaded == false;
        }

        public bool IsReal(ContentCatalogBundle bundle, ContentCatalogFile file)
        {
            return bundle.IsPreloaded || File.Exists(PathOf(bundle, file));
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentCatalogBundle bundle, ContentCatalogFile file)
        {
            if (IsReal(bundle, file))
                return UniTask.CompletedTask;

            Directory.CreateDirectory(_cache);
            return WebContentFetch.File(lifetime, UrlOf(bundle, file), PathOf(bundle, file), bundle.IsGzip);
        }

        public string PathOf(ContentCatalogBundle bundle, ContentCatalogFile file)
        {
            return $"{(bundle.IsPreloaded ? _local : _cache)}/{file.Name}";
        }

        public void Forget(ContentCatalogBundle bundle, ContentCatalogFile file)
        {
            if (bundle.IsPreloaded == false)
                File.Delete(PathOf(bundle, file));
        }

        // What the server holds: the file of the build, .gz for a bundle shipped in gzip.
        public string UrlOf(ContentCatalogBundle bundle, ContentCatalogFile file)
        {
            return $"{_remote}/{ContentCatalogPaths.Shipped(bundle, file)}";
        }
    }
}
#endif
