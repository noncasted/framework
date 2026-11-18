using Cysharp.Threading.Tasks;

namespace Internal
{
    // Where the files of the bundles are on this platform: StreamingAssets read in place (LocalContentBundleStore), or the
    // fetches of a WebGL player (WebContentBundleStore). Main thread only.
    internal interface IContentBundleStore
    {
        // The player fetches the files of the bundle: its progress counts their bytes. False for the files on the disk or in
        // the .data file.
        bool IsFetched(ContentCatalogBundleEntry bundle);

        // The file is on the device: the engine opens it now.
        bool IsReal(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file);

        // Brings the file to PathOf; aborted when the lifetime ends.
        UniTask Fetch(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file);

        // The path AssetBundle.LoadFromFile opens.
        string PathOf(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file);

        // The bundle was unloaded: a fetched copy goes (the cache of the page keeps its bytes for the next fetch).
        void Forget(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file);
    }

    // StreamingAssets of the player as it is (standalone, Android - a path inside the APK that LoadFromFile reads, a test
    // folder of the editor): every file is real, nothing is fetched or forgotten.
    internal sealed class LocalContentBundleStore : IContentBundleStore
    {
        public LocalContentBundleStore(string folder)
        {
            _folder = folder;
        }

        private readonly string _folder;

        public bool IsFetched(ContentCatalogBundleEntry bundle)
        {
            return false;
        }

        public bool IsReal(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return true;
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public string PathOf(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return $"{_folder}/{file.Name}";
        }

        public void Forget(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
        }
    }
}
