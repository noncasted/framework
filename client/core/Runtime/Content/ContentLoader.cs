using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // The loads of a game from one content directory: a registered one in a player (Register), the assets of the project
    // in Play Mode of the editor (EditorContentDirectory). Each load is an AssetLoads entry ("Content: <name>") and a
    // concurrent span of the trace. Main thread only.
    public sealed class ContentLoader : IContentLoader
    {
        internal ContentLoader(IContentDirectory directory)
        {
            _directory = directory;
            _scenes = new ContentSceneLoads(directory, Report);
        }

        private readonly IContentDirectory _directory;
        private readonly ContentSceneLoads _scenes;
        private readonly Dictionary<ContentReference, object> _objects = new();
        private readonly Dictionary<string, ViewableProperty<ContentProgress>> _progress = new();

        public string Name => _directory.Name;

        // Registers the content directory a player build carries (its index in Resources, its files in the store of the
        // platform) for the lifetime: when it ends the directory is unregistered and every object loaded from it is
        // destroyed. remoteUrl: the folder of the content folders on a CDN (a WebGL player only), null for StreamingAssets.
        public static async UniTask<ContentLoader> Register(IReadOnlyLifetime lifetime, string name, string remoteUrl)
        {
            var json = Resources.Load<TextAsset>(ContentPaths.IndexResource(name));

            if (json == null)
                throw new InvalidOperationException($"[Content] No index of the content directory {name} in Resources/{ContentPaths.IndexResource(name)}");

            var index = ContentIndex.FromJson(json.text);
            var directory = await ContentDirectory.Register(lifetime, index, ContentStores.Create(index, remoteUrl));
            return new ContentLoader(directory);
        }

        // The root assets of the directory, loaded with its registration.
        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return _directory.RootAssets<T>();
        }

        public UniTask<T> Load<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object
        {
            reference.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            if (_objects.TryGetValue(reference, out var running))
                return ((ContentObjectLoad<T>)running).Add(lifetime);

            return Create(reference).Start(lifetime);
        }

        public async UniTask<IReadOnlyList<T>> Load<T>(IReadOnlyLifetime lifetime, IReadOnlyList<ContentReference<T>> references) where T : Object
        {
            // Every fetch starts in this frame, so the store sees them as one batch; the engine loads run in parallel.
            var loads = new UniTask<T>[references.Count];

            for (var i = 0; i < references.Count; i++)
                loads[i] = Load(lifetime, references[i]);

            return await UniTask.WhenAll(loads);
        }

        public T LoadNow<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object
        {
            reference.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            if (_objects.TryGetValue(reference, out var running))
            {
                var load = (ContentObjectLoad<T>)running;

                if (load.IsLoaded == false)
                    throw new InvalidOperationException($"[Content] {typeof(T).Name} {reference} is loading asynchronously, LoadNow cannot wait for it");

                load.AddHold(lifetime);
                return reference.Target;
            }

            if (_directory.IsObjectReal(reference.Guid, reference.LocalId) == false)
                throw new InvalidOperationException($"[Content] {typeof(T).Name} {reference} needs a fetch: load it with Load");

            return Create(reference).LoadNow(lifetime);
        }

        public UniTask<ILoadedScene> LoadScene(IReadOnlyLifetime lifetime, ContentScene scene, bool isMain = false)
        {
            scene.ThrowIfEmpty();
            return _scenes.Load(lifetime, scene, isMain);
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentReference reference)
        {
            reference.ThrowIfEmpty();
            return _directory.FetchObject(lifetime, reference.Guid, reference.LocalId, Report(reference));
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene)
        {
            scene.ThrowIfEmpty();
            return _directory.FetchScene(lifetime, scene.Guid, Report(scene));
        }

        public IViewableProperty<ContentProgress> Progress(ContentReference reference)
        {
            reference.ThrowIfEmpty();
            return ProgressOf(ObjectKey(reference), () => _directory.ObjectBytes(reference.Guid, reference.LocalId),
                () => _directory.IsObjectReal(reference.Guid, reference.LocalId));
        }

        public IViewableProperty<ContentProgress> Progress(ContentScene scene)
        {
            scene.ThrowIfEmpty();
            return ProgressOf(scene.Guid, () => _directory.SceneBytes(scene.Guid), () => false);
        }

        private IProgress<long> Report(ContentScene scene)
        {
            return new ContentProgressReport((ViewableProperty<ContentProgress>)Progress(scene));
        }

        private IProgress<long> Report(ContentReference reference)
        {
            return new ContentProgressReport((ViewableProperty<ContentProgress>)Progress(reference));
        }

        private ContentObjectLoad<T> Create<T>(ContentReference<T> reference) where T : Object
        {
            var load = new ContentObjectLoad<T>(reference, _directory, Report(reference), OnObjectOver);
            _objects.Add(reference, load);
            return load;
        }

        private void OnObjectOver(ContentReference reference)
        {
            _objects.Remove(reference);
        }

        // A scene starts at 0 bytes: its first fetch reports the files real already.
        private IViewableProperty<ContentProgress> ProgressOf(string key, Func<long> total, Func<bool> isReal)
        {
            if (_progress.TryGetValue(key, out var progress))
                return progress;

            var bytes = total();
            progress = new ViewableProperty<ContentProgress>(new ContentProgress(isReal() ? bytes : 0, bytes));
            _progress.Add(key, progress);
            return progress;
        }

        private static string ObjectKey(ContentReference reference)
        {
            return $"{reference.Guid}:{reference.LocalId}";
        }
    }
}
