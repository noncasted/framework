#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Internal
{
    // Play Mode of the editor: no bundle is built or loaded. A reference gives the asset of the project (AssetDatabase, by
    // its GUID and local id), never destroyed by its release; a scene is loaded from its asset; the roots are the roots of
    // the ContentBundle asset of the name. Nothing to fetch.
    internal sealed class EditorContentSource : IContentSource
    {
        public EditorContentSource(string name, IReadOnlyList<Object> roots)
        {
            if (roots.Count == 0)
                throw new ArgumentException($"[Content] The content bundle {name} has no roots", nameof(roots));

            Name = name;
            _roots = roots;
        }

        private readonly IReadOnlyList<Object> _roots;

        public string Name { get; }

        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return _roots.OfType<T>().ToArray();
        }

        public async UniTask<T> Load<T>(IReadOnlyLifetime hold, ContentReference<T> reference, IProgress<long> progress) where T : Object
        {
            // A frame, as a load of a player: a caller that cancels at once sees it cancelled.
            await UniTask.Yield(hold.Token);
            return Find(reference);
        }

        public T LoadNow<T>(IReadOnlyLifetime hold, ContentReference<T> reference) where T : Object
        {
            hold.Token.ThrowIfCancellationRequested();
            return Find(reference);
        }

        public UniTask<AsyncOperation> StartScene(IReadOnlyLifetime hold, ContentScene scene, LoadSceneParameters parameters, IProgress<long> progress)
        {
            hold.Token.ThrowIfCancellationRequested();
            var path = scene.EditorPath;

            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException($"[Content] No asset of the {scene}");

            return UniTask.FromResult(EditorSceneManager.LoadSceneAsyncInPlayMode(path, parameters));
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentReference reference, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene, IProgress<long> progress)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public long Bytes(ContentReference reference)
        {
            return 0;
        }

        public long Bytes(ContentScene scene)
        {
            return 0;
        }

        public bool IsReal(ContentReference reference)
        {
            return true;
        }

        private static T Find<T>(ContentReference<T> reference) where T : Object
        {
            var asset = ContentEditorAssets.Find(reference.Guid, reference.LocalId);

            if (asset is GameObject prefab && typeof(Component).IsAssignableFrom(typeof(T)))
                asset = prefab.GetComponent(typeof(T));

            return asset as T ?? throw new InvalidOperationException($"[Content] No {typeof(T).Name} {reference} in the project");
        }
    }
}
#endif
