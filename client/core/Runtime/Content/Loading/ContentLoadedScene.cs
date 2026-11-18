using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // A scene loaded from a content bundle: unloaded by the end of the lifetime of its load or earlier by Unload,
    // once either way.
    internal sealed class ContentLoadedScene : ILoadedScene
    {
        public ContentLoadedScene(Scene scene)
        {
            Scene = scene;
        }

        // Kept so that the second call (the end of the lifetime, then the dispose of the scope) waits for the same unload:
        // a completion source takes several awaiters, an operation of the engine only one.
        private UniTaskCompletionSource _unload;

        public Scene Scene { get; }

        public UniTask Unload()
        {
            if (_unload != null)
                return _unload.Task;

            _unload = new UniTaskCompletionSource();
            Complete(SceneManager.UnloadSceneAsync(Scene)).NoAwait();
            return _unload.Task;
        }

        // On quit Unity unloads the scenes itself.
        public void UnloadOnEnd()
        {
            if (ApplicationQuit.IsQuitting)
                return;

            Unload().NoAwait();
        }

        // The top of a task nobody awaits: the result goes to the awaiters of Unload.
        private async UniTask Complete(AsyncOperation operation)
        {
            try
            {
                await operation;
                _unload.TrySetResult();
            }
            catch (Exception exception)
            {
                _unload.TrySetException(exception);
            }
        }
    }
}
