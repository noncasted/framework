using Cysharp.Threading.Tasks;

namespace Internal
{
    // Where the files of the bundles are on this platform: StreamingAssets read in place (LocalContentDistribution), or the
    // fetches of a WebGL player (WebContentDistribution). Main thread only.
    internal interface IContentDistribution
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
}
