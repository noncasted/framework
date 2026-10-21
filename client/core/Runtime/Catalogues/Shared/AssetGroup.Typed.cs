using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Internal
{
    // A group whose content lives in a single addressable ScriptableObject: the handle is held here,
    // the generated classes only have to unpack the fields.
    public abstract class AssetGroup<TAsset> : AssetGroup where TAsset : Object
    {
        private AsyncOperationHandle<TAsset> _handle;
        private int _handleSession;

        protected TAsset Asset { get; private set; }

        protected async UniTask LoadAsset(string address)
        {
            _handle = Addressables.LoadAssetAsync<TAsset>(address);
            _handleSession = Session;
            Asset = await _handle.ToUniTask();
        }

        protected void LoadResource(string path)
        {
            Asset = Resources.Load<TAsset>(path);

            if (Asset == null)
                throw new System.InvalidOperationException($"{Name} is missing at Resources/{path}");
        }

        protected void UnloadAsset()
        {
            Asset = null;

            if (_handleSession == Session && _handle.IsValid())
                Addressables.Release(_handle);

            _handle = default;
        }
    }
}