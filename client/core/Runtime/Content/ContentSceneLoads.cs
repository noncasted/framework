using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Internal
{
    // The scenes of a content directory: each load is its own additive instance, unloaded when its lifetime ends. A
    // lifetime that ends during the load gives OperationCanceledException; a scene the engine has started loading is
    // loaded to the end and unloaded at once.
    internal sealed class ContentSceneLoads
    {
        public ContentSceneLoads(IContentDirectory directory, Func<ContentScene, IProgress<long>> report)
        {
            _directory = directory;
            _report = report;
        }

        private readonly IContentDirectory _directory;
        private readonly Func<ContentScene, IProgress<long>> _report;

        public async UniTask<ILoadedScene> Load(IReadOnlyLifetime lifetime, ContentScene scene, bool isMain)
        {
            scene.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            var load = AssetLoads.Begin($"Content: scene {scene.Guid}");
            using var span = GameProfiler.Concurrent("Content: scene");
            ContentLoadedScene loaded;

            try
            {
                await _directory.FetchScene(lifetime, scene.Guid, _report(scene));
                lifetime.Token.ThrowIfCancellationRequested();

                var operation = SceneManager.LoadSceneAsync(scene.Id, new LoadSceneParameters(LoadSceneMode.Additive));

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
                throw;
            }

            load.Finish($"Content: scene {loaded.Scene.name}");

            if (lifetime.IsTerminated)
            {
                loaded.Unload().NoAwait();
                throw new OperationCanceledException(lifetime.Token);
            }

            lifetime.Listen(loaded.UnloadOnEnd);

            if (isMain)
                SceneManager.SetActiveScene(loaded.Scene);

            return loaded;
        }
    }
}
