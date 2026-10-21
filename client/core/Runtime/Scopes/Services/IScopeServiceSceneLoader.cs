using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Internal
{
    /// <summary>
    /// Where the scope gets the scene for its objects from.
    /// </summary>
    public interface IScopeServiceSceneLoader
    {
        /// <summary>
        /// Scope name in the profiler trace and in the container.
        /// </summary>
        string Name { get; }

        UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader);
    }

    /// <summary>
    /// The services scene is needed only as a container for the scope objects, so it is created
    /// on the fly: an empty scene in a bundle costs a file open and a couple of frames of async loading,
    /// while its payload is zero.
    /// </summary>
    public class RuntimeScene : IScopeServiceSceneLoader
    {
        public RuntimeScene(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            var scene = loader.Create(Name);
            return UniTask.FromResult<IServiceScopeBinder>(new ServiceScopeBinder(scene.Scene));
        }
    }

    /// <summary>
    /// The services scene is an asset with content the scope needs.
    /// </summary>
    public class AssetScene : IScopeServiceSceneLoader
    {
        public AssetScene(AssetReference asset)
        {
            _asset = asset;
        }

        private readonly AssetReference _asset;

        public string Name => _asset.AssetGUID;

        public async UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            var scene = await loader.Load(_asset);
            return new ServiceScopeBinder(scene.Scene);
        }
    }

    /// <summary>
    /// The scope has no scene of its own: there is nowhere to move objects, the binder throws when used.
    /// </summary>
    public class NoScene : IScopeServiceSceneLoader
    {
        public string Name => "Registry";

        public UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            return UniTask.FromResult<IServiceScopeBinder>(new ExceptionServiceScopeBinder(Name));
        }
    }
}