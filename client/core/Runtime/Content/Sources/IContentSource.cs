using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    // What a ContentLoader loads from: the bundles of a player (BundleContentSource) or the assets of the project in Play
    // Mode of the editor (EditorContentSource). A load holds what it needs while its hold lives. Main thread only.
    internal interface IContentSource
    {
        // The bundle of the loader: its roots.
        string Name { get; }

        IReadOnlyList<T> RootAssets<T>() where T : Object;

        // Fetches and loads the bundle of the target with its dependencies, then the target; they stay while the hold lives.
        UniTask<T> Load<T>(IReadOnlyLifetime hold, ContentReference<T> reference, IProgress<long> progress) where T : Object;

        // Synchronous: throws when the bundle needs a fetch or loads asynchronously.
        T LoadNow<T>(IReadOnlyLifetime hold, ContentReference<T> reference) where T : Object;

        // Holds the bundle of the scene and starts the load of the engine.
        UniTask<AsyncOperation> StartScene(IReadOnlyLifetime hold, ContentScene scene, LoadSceneParameters parameters, IProgress<long> progress);

        UniTask Fetch(IReadOnlyLifetime lifetime, ContentReference reference, IProgress<long> progress);
        UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene, IProgress<long> progress);

        // The bytes fetched for the target: 0 when nothing is ever fetched.
        long Bytes(ContentReference reference);
        long Bytes(ContentScene scene);

        bool IsReal(ContentReference reference);
    }
}
