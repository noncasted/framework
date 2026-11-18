using Cysharp.Threading.Tasks;

namespace Internal
{
    // StreamingAssets of the player as it is (standalone, Android - a path inside the APK that LoadFromFile reads, a test
    // folder of the editor): every file is real, nothing is fetched or forgotten.
    internal sealed class LocalContentDistribution : IContentDistribution
    {
        public LocalContentDistribution(string folder)
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
