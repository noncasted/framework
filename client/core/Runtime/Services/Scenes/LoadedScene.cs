using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Internal
{
    public interface ILoadedScene
    {
        Scene Scene { get; }

        UniTask Unload();
    }

    public class LoadedScene : ILoadedScene
    {
        public LoadedScene(SceneInstance instance)
        {
            _instance = instance;
        }

        private readonly SceneInstance _instance;

        public Scene Scene => _instance.Scene;

        public UniTask Unload()
        {
            return Addressables.UnloadSceneAsync(_instance).ToUniTask();
        }
    }

    /// <summary>
    /// A scene created at runtime instead of loading an empty asset: the services container
    /// needs only the <see cref="Scene"/> itself, while a bundle with zero content costs
    /// a file open and a couple of frames of an async operation.
    /// </summary>
    public class CreatedScene : ILoadedScene
    {
        public CreatedScene(Scene scene)
        {
            Scene = scene;
        }

        public Scene Scene { get; }

        public UniTask Unload()
        {
            if (Scene.isLoaded != true)
                return UniTask.CompletedTask;

            return SceneManager.UnloadSceneAsync(Scene).ToUniTask();
        }
    }
}