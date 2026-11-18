using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    // A bundle of a player build as the source of its loader: its roots, loaded by the registration and held while it
    // lives, and every lazy target of the catalog, wherever its bundle is.
    internal sealed class BundleContentSource : IContentSource
    {
        private BundleContentSource(ContentBundleStorage storage, string name, IReadOnlyList<Object> roots)
        {
            _storage = storage;
            Name = name;
            _roots = roots;
        }

        private readonly ContentBundleStorage _storage;
        private readonly IReadOnlyList<Object> _roots;

        public string Name { get; }

        // Holds the bundle (fetched first when the player fetches it) while the lifetime lives and loads its roots.
        public static async UniTask<BundleContentSource> Register(IReadOnlyLifetime lifetime, ContentBundleStorage storage, string name)
        {
            await storage.Hold(lifetime, name, null);
            lifetime.Token.ThrowIfCancellationRequested();

            var bundle = storage.Catalog.Bundle(name);
            var assets = storage.Bundle(name).Assets;
            var roots = new Object[bundle.Roots.Count];

            using (GameProfiler.Scope($"Content: roots {name}"))
            {
                for (var i = 0; i < roots.Length; i++)
                {
                    roots[i] = assets.LoadAsset<Object>(bundle.Roots[i]) ??
                               throw new InvalidOperationException($"[Content] The root {bundle.Roots[i]} is not in the content bundle {name}");
                }
            }

            return new BundleContentSource(storage, bundle.Name, roots);
        }

        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            var roots = new List<T>();

            foreach (var root in _roots)
            {
                if (root is T typed)
                    roots.Add(typed);
            }

            return roots;
        }

        public async UniTask<T> Load<T>(IReadOnlyLifetime hold, ContentReference<T> reference, IProgress<long> progress) where T : Object
        {
            var entry = _storage.Catalog.Object(reference.Guid, reference.LocalId);
            await _storage.Hold(hold, entry.Bundle, progress);
            hold.Token.ThrowIfCancellationRequested();

            var assets = _storage.Bundle(entry.Bundle).Assets;

            if (entry.IsSubAsset)
            {
                var request = assets.LoadAssetWithSubAssetsAsync<T>(entry.Guid);
                await ((AsyncOperation)request).ToUniTask();
                return Pick<T>(request.allAssets, entry, reference);
            }

            var main = assets.LoadAssetAsync(entry.Guid, ContentTarget.AssetType<T>());
            await ((AsyncOperation)main).ToUniTask();
            return Target<T>(main.asset, reference);
        }

        public T LoadNow<T>(IReadOnlyLifetime hold, ContentReference<T> reference) where T : Object
        {
            var entry = _storage.Catalog.Object(reference.Guid, reference.LocalId);
            _storage.HoldNow(hold, entry.Bundle);

            var assets = _storage.Bundle(entry.Bundle).Assets;

            using (GameProfiler.Scope($"Content: load {typeof(T).Name}"))
            {
                if (entry.IsSubAsset)
                    return Pick<T>(assets.LoadAssetWithSubAssets<T>(entry.Guid), entry, reference);

                return Target<T>(assets.LoadAsset(entry.Guid, ContentTarget.AssetType<T>()), reference);
            }
        }

        public async UniTask<AsyncOperation> StartScene(IReadOnlyLifetime hold, ContentScene scene, LoadSceneParameters parameters, IProgress<long> progress)
        {
            var entry = _storage.Catalog.Scene(scene.Guid);
            await _storage.Hold(hold, entry.Bundle, progress);
            hold.Token.ThrowIfCancellationRequested();
            return SceneManager.LoadSceneAsync(entry.Path, parameters);
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentReference reference, IProgress<long> progress)
        {
            return _storage.Fetch(lifetime, _storage.Catalog.Object(reference.Guid, reference.LocalId).Bundle, progress);
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene, IProgress<long> progress)
        {
            return _storage.Fetch(lifetime, _storage.Catalog.Scene(scene.Guid).Bundle, progress);
        }

        public long Bytes(ContentReference reference)
        {
            return _storage.Bytes(_storage.Catalog.Object(reference.Guid, reference.LocalId).Bundle);
        }

        public long Bytes(ContentScene scene)
        {
            return _storage.Bytes(_storage.Catalog.Scene(scene.Guid).Bundle);
        }

        public bool IsReal(ContentReference reference)
        {
            return _storage.IsReal(_storage.Catalog.Object(reference.Guid, reference.LocalId).Bundle);
        }

        private static T Target<T>(Object asset, ContentReference<T> reference) where T : Object
        {
            return ContentTarget.Of<T>(asset) ?? throw new InvalidOperationException($"[Content] The engine did not load {typeof(T).Name} {reference}");
        }

        private static T Pick<T>(Object[] assets, ContentCatalogObjectEntry entry, ContentReference<T> reference) where T : Object
        {
            foreach (var asset in assets)
            {
                if (asset is T typed && asset.name == entry.SubName)
                    return typed;
            }

            throw new InvalidOperationException($"[Content] No {typeof(T).Name} {entry.SubName} among the sub-assets of {reference}");
        }
    }
}
