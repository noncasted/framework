using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // A group whose content lives in a single ScriptableObject of a content bundle, referenced by the ContentRoot
    // under the GUID of the asset: the load lifetime is held here, the generated classes only have to unpack the fields.
    public abstract class AssetGroup<TAsset> : AssetGroup where TAsset : ScriptableObject
    {
        private ILifetime _lifetime;
        private int _lifetimeSession;

        protected TAsset Asset { get; private set; }

        // Fetches the files of the group asset when they are not real yet, then loads it.
        protected async UniTask LoadAsset(string guid)
        {
            var reference = CatalogContent.Group(guid);
            var lifetime = BeginLoad();
            Asset = Cast(guid, await CatalogContent.Loader.Load(lifetime, reference));
        }

        // A preloaded group: its files are real since AddContent, so the load is synchronous.
        protected void LoadPreloadedAsset(string guid)
        {
            var reference = CatalogContent.Group(guid);
            var lifetime = BeginLoad();
            Asset = Cast(guid, CatalogContent.Loader.LoadNow(lifetime, reference));
        }

        // Without the asset the load has failed: the generated UnloadGroup unpacks the asset (an audio group releases
        // its decoded clips), here only the load lifetime ends.
        protected override void UnloadFailedGroup()
        {
            if (Asset == null)
            {
                UnloadAsset();
                return;
            }

            UnloadGroup();
        }

        // Ends the load lifetime: the content loader releases the asset. A lifetime of a previous run belongs to a
        // loader that is gone and is dropped.
        protected void UnloadAsset()
        {
            Asset = null;

            if (_lifetime != null && _lifetimeSession == Session)
                _lifetime.Terminate();

            _lifetime = null;
        }

        private ILifetime BeginLoad()
        {
            _lifetime = CatalogContent.Lifetime.Child();
            _lifetimeSession = Session;
            return _lifetime;
        }

        private TAsset Cast(string guid, ScriptableObject asset)
        {
            if (asset is TAsset typed)
                return typed;

            throw new InvalidOperationException(
                $"{Name}: the group {guid} of the ContentRoot is a {asset.GetType().Name}, not a {typeof(TAsset).Name}");
        }
    }
}
