using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // The content of a root scope: how the bundle of a name becomes the source of its loader (the bundles of the build in
    // a player, the assets of the project in Play Mode of the editor), the loads its loaders share and the loaders of its
    // bundles (the main one of AddContent, the ones of AddContentBundle). Lives as long as the root.
    public sealed class ContentEntryPoint
    {
        private ContentEntryPoint(Func<IReadOnlyLifetime, string, UniTask<IContentSource>> register)
        {
            _register = register;
        }

        private readonly Func<IReadOnlyLifetime, string, UniTask<IContentSource>> _register;

        internal ContentLoads Loads { get; } = new();
        internal ContentLoadersCollection Loaders { get; } = new();

        // The main bundle of the build (a player) or of the project (Play Mode), held while the lifetime lives, without a
        // scope (a smoke player, a tool); a game calls AddContent.
        public static UniTask<ContentLoader> RegisterMain(IReadOnlyLifetime lifetime, ContentOptions options)
        {
            options.Validate();
            return Open(options).Register(lifetime, options.Name);
        }

        // The content of this player (or of Play Mode).
        internal static ContentEntryPoint Open(ContentOptions options)
        {
#if UNITY_EDITOR
            return new ContentEntryPoint((_, name) => UniTask.FromResult(EditorContentSources.Create(name)));
#else
            ContentCatalog catalog;

            using (GameProfiler.Scope("Content: catalog"))
                catalog = ContentCatalog.Load();

            return Over(new ContentBundleSet(catalog, Store(catalog, options)));
#endif
        }

        // The bundles of a catalog over a store (a player; a test over bundles built into a folder).
        internal static ContentEntryPoint Over(ContentBundleSet set)
        {
            return new ContentEntryPoint(async (lifetime, name) => await BundleContentSource.Register(lifetime, set, name));
        }

        // The bundle loaded whole and held while the lifetime lives, and a loader over its roots.
        internal async UniTask<ContentLoader> Register(IReadOnlyLifetime lifetime, string name)
        {
            return new ContentLoader(await _register(lifetime, name), Loads);
        }

        // The main bundle of AddContent: registered at once, its loader among the loaders of the root.
        internal async UniTask<ContentLoader> AddMain(IReadOnlyLifetime lifetime, string name)
        {
            var loader = await Register(lifetime, name);
            Loaders.Add(name, loader);
            return loader;
        }

        // One more bundle of AddContentBundle, registered in the background (OnFirstWait: at the first Wait of its name)
        // and held while the lifetime lives; Loaders gives its loader or its failure.
        internal void AddBundle(IReadOnlyLifetime lifetime, string name, ContentRegistration registration)
        {
            if (registration == ContentRegistration.OnFirstWait)
            {
                Loaders.Defer(name, source => RegisterInBackground(lifetime, name, source).NoAwait());
                return;
            }

            RegisterInBackground(lifetime, name, Loaders.Begin(name)).NoAwait();
        }

        // The top of a task nobody awaits: a failure goes to the log and to every Wait of the bundle.
        private async UniTask RegisterInBackground(IReadOnlyLifetime lifetime, string name, UniTaskCompletionSource<IContentLoader> registration)
        {
            try
            {
                registration.TrySetResult(await Register(lifetime, name));
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated)
            {
                registration.TrySetCanceled();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                registration.TrySetException(exception);
            }
        }

        private static IContentBundleStore Store(ContentCatalog catalog, ContentOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebContentFetch.Limit(options.MaxFetches);

            var store = new WebContentBundleStore(Application.streamingAssetsPath, options.RemoteUrl, Application.temporaryCachePath);
            var urls = new List<string>();

            foreach (var bundle in catalog.Bundles)
            {
                if (bundle.IsPreloaded)
                    continue;

                foreach (var file in bundle.Files)
                    urls.Add(store.UrlOf(bundle, file));
            }

            WebContentFetch.KeepOnly(urls);
            return store;
#else
            return new LocalContentBundleStore($"{Application.streamingAssetsPath}/{ContentCatalogPaths.Folder}");
#endif
        }
    }
}
