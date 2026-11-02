using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public static class RootScopeExtensions
    {
        // Loads the root of the whole scope chain: the project's Construct (registers the scope loaders, options and
        // whatever the game puts at the root). The scope lives until the application quits.
        public static async UniTask<ILoadedScope> LoadRoot(this IServiceScopeLoader loader, Func<IScopeBuilder, UniTask> construct)
        {
            var options = new ScopeLoadOptions(null, construct);

            using var stage = GameProfiler.Scope("Startup");

            var scope = await loader.Load(options);
            await scope.Initialize();

            DisposeOnQuit(scope);

            return scope;
        }

        // In the editor quitting arrives when scene objects are already destroyed, and the Updater manages
        // to run a frame with dead UI. ExitingPlayMode arrives before the scenes are destroyed, so
        // the scope closes on it; quitting remains for the build.
        private static void DisposeOnQuit(ILoadedScope scope)
        {
            var isDisposed = false;

            Application.quitting += Dispose;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
            {
                if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode)
                    return;

                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                Dispose();
            }
#endif

            void Dispose()
            {
                Application.quitting -= Dispose;

                if (isDisposed == true)
                    return;

                isDisposed = true;

                // Before the lifetimes terminate: the scopes they dispose must not unload their scenes.
                ApplicationQuit.Mark();

                Debug.Log("Root scope lifetime terminated, disposing loaded scope.");
                scope.Dispose().NoAwait();
            }
        }
    }
}
