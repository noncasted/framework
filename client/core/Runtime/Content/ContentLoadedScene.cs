using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Internal
{
    // A scene loaded from a content directory: unloaded by the end of the lifetime of its load or earlier by Unload,
    // once either way.
    internal sealed class ContentLoadedScene : ILoadedScene
    {
        public ContentLoadedScene(Scene scene)
        {
            Scene = scene;
        }

        private bool _isUnloaded;

        public Scene Scene { get; }

        public UniTask Unload()
        {
            if (_isUnloaded)
                return UniTask.CompletedTask;

            _isUnloaded = true;
            return SceneManager.UnloadSceneAsync(Scene).ToUniTask();
        }

        public void UnloadOnEnd()
        {
            Unload().NoAwait();
        }
    }
}
