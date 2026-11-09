using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    // Loads the objects and scenes of the content directory on a lifetime, which owns the result: an object is released,
    // a scene unloaded when it ends. A lifetime that ends during the load gives OperationCanceledException; a load the
    // engine has started goes on and is released at once. An empty reference throws.
    public interface IContentLoader
    {
        // Fetches the files the object needs that are not real yet, then loads it.
        UniTask<T> Load<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object;

        // Several at once: the fetches start together, then the loads go in parallel.
        UniTask<IReadOnlyList<T>> Load<T>(IReadOnlyLifetime lifetime, IReadOnlyList<ContentReference<T>> references) where T : Object;

        // Synchronous, only for an object whose files are real already (preloaded, on the disk, fetched): throws for one
        // that needs a fetch instead of waiting.
        T LoadNow<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object;

        // Additive; isMain makes it the active scene.
        UniTask<ILoadedScene> LoadScene(IReadOnlyLifetime lifetime, ContentScene scene, bool isMain = false);

        // Only the files, no objects: warms the next step.
        UniTask Fetch(IReadOnlyLifetime lifetime, ContentReference reference);
        UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene);

        // The fetch of the files of one load, for a loading screen.
        IViewableProperty<ContentProgress> Progress(ContentReference reference);
        IViewableProperty<ContentProgress> Progress(ContentScene scene);
    }
}
