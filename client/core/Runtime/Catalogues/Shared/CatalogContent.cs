using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // The content the generated catalogues load from: the content loader and the ContentRoot of the root scope, bound by
    // AddContent for the lifetime of the root. Static because the catalogues are: their groups and scenes are statics
    // read from anywhere (Sprites.X, Scenes.X), with no scope at hand. Unbound, every access throws.
    [NoAutoStaticsCleanup]
    public static class CatalogContent
    {
        private static IContentLoader _loader;
        private static IReadOnlyLifetime _lifetime;
        private static ContentRoot _root;

        internal static IContentLoader Loader => _loader ?? throw NotBound();

        // The lifetime of the root scope: the load lifetimes of the groups are its children.
        internal static IReadOnlyLifetime Lifetime => _lifetime ?? throw NotBound();

        // A scene of the scene folders (the generated Scenes catalogue).
        public static ContentScene Scene(string guid)
        {
            return Root.Scene(guid);
        }

        internal static ContentReference<ScriptableObject> Group(string guid)
        {
            return Root.Group(guid);
        }

        // At most one ContentRoot among the roots of the directory; a directory without one has no catalogues. The files
        // of the preloaded groups are fetched here, so their first access is synchronous.
        internal static async UniTask Bind(IReadOnlyLifetime lifetime, IContentLoader loader, IReadOnlyList<ContentRoot> roots)
        {
            if (_loader != null)
                throw new InvalidOperationException("[CatalogContent] The catalogues are bound already: one root scope with AddContent at a time");

            if (roots.Count > 1)
                throw new InvalidOperationException($"[CatalogContent] The content directory has {roots.Count} ContentRoot assets among its roots, one expected");

            _loader = loader;
            _lifetime = lifetime;
            _root = roots.Count == 1 ? roots[0] : null;
            lifetime.Listen(Unbind);

            if (_root == null)
                return;

            var fetches = new List<UniTask>();

            foreach (var group in _root.PreloadedGroups)
                fetches.Add(loader.Fetch(lifetime, group));

            await UniTask.WhenAll(fetches);
        }

        private static ContentRoot Root
        {
            get
            {
                if (_loader == null)
                    throw NotBound();

                if (_root == null)
                    throw new InvalidOperationException(
                        "[CatalogContent] The content directory has no ContentRoot: add the generated ContentRoot asset to Content.Roots of ProjectSettings/Framework.json");

                return _root;
            }
        }

        private static void Unbind()
        {
            _loader = null;
            _lifetime = null;
            _root = null;
            AssetGroup.UnloadAll();
        }

        private static Exception NotBound()
        {
            return new InvalidOperationException("[CatalogContent] The catalogues are not bound: call AddContent in the root scope first");
        }

        // Without a domain reload the statics survive exiting Play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _loader = null;
            _lifetime = null;
            _root = null;
        }
    }
}
