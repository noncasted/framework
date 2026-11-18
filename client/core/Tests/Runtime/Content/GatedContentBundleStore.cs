using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    // A store that fetches like a WebGL player over the bundles of ContentBundleTestBuild: a file is real once it is copied
    // from the shipped folder into a folder of its own; with the gate shut a fetch waits for Open. Counts the fetches, the
    // aborts and the files forgotten after an unload.
    internal sealed class GatedContentBundleStore : IContentBundleStore
    {
        public GatedContentBundleStore(string source, string folder, bool isGated)
        {
            _source = source;
            _folder = folder;
            IsGated = isGated;
            Directory.CreateDirectory(folder);
        }

        private readonly string _source;
        private readonly string _folder;
        private readonly UniTaskCompletionSource _gate = new();

        // Shut: a fetch waits for Open.
        public bool IsGated { get; private set; }

        public int Fetches { get; private set; }
        public int Aborts { get; private set; }
        public int Forgotten { get; private set; }

        public void Shut()
        {
            IsGated = true;
        }

        public void Open()
        {
            _gate.TrySetResult();
        }

        public bool IsFetched(ContentCatalogBundleEntry bundle)
        {
            return true;
        }

        public bool IsReal(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return File.Exists(PathOf(bundle, file));
        }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            Fetches++;

            try
            {
                if (IsGated)
                    await _gate.Task.AttachExternalCancellation(lifetime.Token);
                else
                    await UniTask.Yield(lifetime.Token);
            }
            catch (System.OperationCanceledException)
            {
                Aborts++;
                throw;
            }

            File.Copy($"{_source}/{file.Name}", PathOf(bundle, file));
        }

        public string PathOf(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            return $"{_folder}/{file.Name}";
        }

        public void Forget(ContentCatalogBundleEntry bundle, ContentCatalogFileEntry file)
        {
            Forgotten++;
            File.Delete(PathOf(bundle, file));
        }
    }
}
