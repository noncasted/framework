using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // One bundle of the engine and the lifetimes holding it: loaded once for all of them, unloaded with its objects
    // (AssetBundle.Unload(true)) when the last one ends. A load the last holder left is abandoned: its fetch aborted,
    // a load of the engine already started finished and unloaded at once; a load that comes meanwhile waits for that
    // unload (the engine refuses two loads of one file). Main thread only.
    internal sealed class ContentBundleState
    {
        public ContentBundleState(ContentCatalogBundleEntry bundle, IContentBundleStore store,
            Func<IReadOnlyLifetime, ContentCatalogBundleEntry, UniTask> fetch)
        {
            _bundle = bundle;
            _store = store;
            _fetch = fetch;
        }

        private readonly ContentCatalogBundleEntry _bundle;
        private readonly IContentBundleStore _store;
        private readonly Func<IReadOnlyLifetime, ContentCatalogBundleEntry, UniTask> _fetch;
        private readonly List<AssetBundle> _loaded = new();
        private int _holds;
        private Lifetime _load;

        // The holders of a load wait on a completion source (a UniTask takes one awaiter at a time); an abandoned load
        // completes its unload, which never throws.
        private UniTaskCompletionSource _loading;
        private UniTaskCompletionSource _abandoned;

        public bool IsLoaded => _loaded.Count > 0;

        // The bundle of the assets (not of the scenes), while loaded.
        public AssetBundle Assets
        {
            get
            {
                foreach (var bundle in _loaded)
                {
                    if (bundle.isStreamedSceneAssetBundle == false)
                        return bundle;
                }

                throw new InvalidOperationException($"[Content] The content bundle {_bundle.Name} is {(IsLoaded ? "loaded without assets: it has scenes only" : "not loaded")}");
            }
        }

        public UniTask Hold(IReadOnlyLifetime hold)
        {
            hold.Token.ThrowIfCancellationRequested();
            Add(hold);

            if (IsLoaded)
                return UniTask.CompletedTask;

            if (_load == null)
            {
                _load = new Lifetime();
                _loading = new UniTaskCompletionSource();
                var unwound = new UniTaskCompletionSource();
                Run(_load, _abandoned, _loading, unwound).Forget();
                _abandoned = unwound;
            }

            return _loading.Task.AttachExternalCancellation(hold.Token);
        }

        // Synchronous: the files are real and no load of the bundle runs.
        public void HoldNow(IReadOnlyLifetime hold)
        {
            hold.Token.ThrowIfCancellationRequested();

            if (IsLoaded == false)
            {
                if (_load != null || (_abandoned != null && _abandoned.Task.Status == UniTaskStatus.Pending))
                    throw new InvalidOperationException($"[Content] The content bundle {_bundle.Name} is loading asynchronously, LoadNow cannot wait for it");

                foreach (var file in _bundle.Files)
                {
                    if (_store.IsReal(_bundle, file) == false)
                        throw new InvalidOperationException($"[Content] The content bundle {_bundle.Name} needs a fetch: load it with Load");
                }

                using (GameProfiler.Scope($"Content: bundle {_bundle.Name}"))
                {
                    foreach (var file in _bundle.Files)
                        _loaded.Add(Checked(AssetBundle.LoadFromFile(_store.PathOf(_bundle, file)), file));
                }
            }

            Add(hold);
        }

        private void Add(IReadOnlyLifetime hold)
        {
            _holds++;
            hold.Listen(Release);
        }

        private void Release()
        {
            _holds--;

            if (_holds > 0)
                return;

            if (_load != null)
            {
                // Abandoned: the load unloads what it got itself; the next one waits for that (_abandoned).
                _load.Terminate();
                _load = null;
                return;
            }

            Unload(_loaded);
            _loaded.Clear();
        }

        // The top of a task nobody awaits: the outcome goes to the holders through loading; unwound completes when the
        // load is over, its bundles kept or unloaded.
        private async UniTaskVoid Run(Lifetime lifetime, UniTaskCompletionSource previous, UniTaskCompletionSource loading, UniTaskCompletionSource unwound)
        {
            Exception failure = null;

            try
            {
                if (previous != null)
                    await previous.Task;

                await LoadFiles(lifetime);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            // Over before the holders hear of it: one that ends at once releases a loaded bundle, not a load. A failed
            // load is no load: the next hold tries again; an abandoned one is replaced already.
            if (_load == lifetime)
                _load = null;

            unwound.TrySetResult();

            if (failure is OperationCanceledException)
                loading.TrySetCanceled();
            else if (failure != null)
                loading.TrySetException(failure);
            else
                loading.TrySetResult();
        }

        private async UniTask LoadFiles(Lifetime lifetime)
        {
            using var span = GameProfiler.Concurrent($"Content: bundle {_bundle.Name}");

            await _fetch(lifetime, _bundle);
            lifetime.Token.ThrowIfCancellationRequested();

            var loaded = new List<AssetBundle>();

            try
            {
                foreach (var file in _bundle.Files)
                {
                    // Not cancelled: the engine finishes a load it started.
                    var request = AssetBundle.LoadFromFileAsync(_store.PathOf(_bundle, file));
                    await ((AsyncOperation)request).ToUniTask();
                    loaded.Add(Checked(request.assetBundle, file));
                }
            }
            catch (Exception)
            {
                Unload(loaded);
                throw;
            }

            if (lifetime.IsTerminated)
            {
                Unload(loaded);
                throw new OperationCanceledException(lifetime.Token);
            }

            _loaded.AddRange(loaded);
        }

        private void Unload(List<AssetBundle> bundles)
        {
            foreach (var bundle in bundles)
                bundle.Unload(true);

            foreach (var file in _bundle.Files)
                _store.Forget(_bundle, file);
        }

        private AssetBundle Checked(AssetBundle bundle, ContentCatalogFileEntry file)
        {
            if (bundle == null)
                throw new InvalidOperationException($"[Content] The engine did not load {file.Name} of the content bundle {_bundle.Name} from {_store.PathOf(_bundle, file)}");

            return bundle;
        }
    }
}
