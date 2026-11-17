using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // The scenes of the content: each load is its own additive instance, unloaded when its lifetime ends, its bundle let
    // go after the unload (the engine would destroy the objects of a scene still loaded). A lifetime that ends during the
    // load gives OperationCanceledException; a scene the engine has started loading is loaded to the end and unloaded at once.
    internal sealed class ContentSceneLoads
    {
        public ContentSceneLoads(IContentSource source, Func<ContentScene, IProgress<long>> report)
        {
            _source = source;
            _report = report;
        }

        private readonly IContentSource _source;
        private readonly Func<ContentScene, IProgress<long>> _report;

        public async UniTask<ILoadedScene> Load(IReadOnlyLifetime lifetime, ContentScene scene, bool isMain)
        {
            scene.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            var load = AssetLoads.Begin($"Content: scene {scene.Guid}");
            using var span = GameProfiler.Concurrent("Content: scene");
            var hold = new Lifetime();
            ContentLoadedScene loaded;

            // Until the engine starts the scene the end of the lifetime lets its bundle go; after that the scene loads.
            Action abort = hold.Terminate;
            lifetime.Listen(abort);

            try
            {
                AsyncOperation operation;

                try
                {
                    operation = await _source.StartScene(hold, scene, new LoadSceneParameters(LoadSceneMode.Additive), _report(scene));
                }
                finally
                {
                    if (lifetime.IsTerminated == false)
                        lifetime.RemoveListener(abort);
                }

                if (operation == null)
                    throw new InvalidOperationException($"[Content] The engine refused to load the {scene}");

                // The engine adds the scene to the list at once, loaded later: the last one is this load.
                loaded = new ContentLoadedScene(SceneManager.GetSceneAt(SceneManager.sceneCount - 1));
                await operation.ToUniTask();

                if (loaded.Scene.isLoaded == false)
                    throw new InvalidOperationException($"[Content] The {scene} did not load");
            }
            catch (Exception exception)
            {
                load.Fail(exception);
                hold.Terminate();
                throw;
            }

            load.Finish($"Content: scene {loaded.Scene.name}");
            span.SetName($"Content: scene {loaded.Scene.name}");

            if (lifetime.IsTerminated)
            {
                UnloadThenRelease(loaded, hold).NoAwait();
                throw new OperationCanceledException(lifetime.Token);
            }

            lifetime.Listen(() =>
            {
                // On quit Unity unloads the scenes and the bundles itself.
                if (ApplicationQuit.IsQuitting == false)
                    UnloadThenRelease(loaded, hold).NoAwait();
            });

            if (isMain)
                SceneManager.SetActiveScene(loaded.Scene);

            return loaded;
        }

        private static async UniTask UnloadThenRelease(ContentLoadedScene loaded, Lifetime hold)
        {
            try
            {
                await loaded.Unload();
            }
            finally
            {
                hold.Terminate();
            }
        }
    }
}
