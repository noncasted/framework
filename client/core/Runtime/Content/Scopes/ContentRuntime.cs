using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // The content of a root scope: how the bundle of a name becomes the source of its loader (the bundles of the build in
    // a player, the assets of the project in Play Mode of the editor), the loads its loaders share and the registry of
    // its bundles. Lives as long as the root.
    internal sealed class ContentRuntime
    {
        private ContentRuntime(Func<IReadOnlyLifetime, string, UniTask<IContentSource>> register)
        {
            _register = register;
        }

        private readonly Func<IReadOnlyLifetime, string, UniTask<IContentSource>> _register;

        public ContentLoads Loads { get; } = new();
        public ContentBundles Bundles { get; } = new();

        // The content of this player (or of Play Mode).
        public static ContentRuntime Open(ContentOptions options)
        {
#if UNITY_EDITOR
            return new ContentRuntime((_, name) => UniTask.FromResult(EditorContentSources.Create(name)));
#else
            ContentCatalog catalog;

            using (GameProfiler.Scope("Content: catalog"))
                catalog = ContentCatalog.Load();

            return Over(new ContentBundleSet(catalog, Store(catalog, options)));
#endif
        }

        // The bundles of a catalog over a store (a player; a test over bundles built into a folder).
        public static ContentRuntime Over(ContentBundleSet set)
        {
            return new ContentRuntime(async (lifetime, name) => await BundleContentSource.Register(lifetime, set, name));
        }

        // The bundle loaded whole and held while the lifetime lives, and a loader over its roots.
        public async UniTask<ContentLoader> Register(IReadOnlyLifetime lifetime, string name)
        {
            return new ContentLoader(await _register(lifetime, name), Loads);
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
