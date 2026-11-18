using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // The loads of a game through one bundle (its roots) of a player (BundleContentSource) or the assets of the project
    // in Play Mode of the editor (EditorContentSource): any reference of the catalog, wherever its bundle is. Each load is
    // an AssetLoads entry ("Content: <name>") and a concurrent span of the trace. Main thread only.
    public sealed class ContentLoader : IContentLoader
    {
        internal ContentLoader(IContentSource source, ContentLoadsCollection loads)
        {
            _source = source;
            _loads = loads;
            _scenes = new ContentSceneLoader(source, Report);
        }

        private readonly IContentSource _source;
        private readonly ContentLoadsCollection _loads;
        private readonly ContentSceneLoader _scenes;

        public string Name => _source.Name;

        // The root assets of the bundle, loaded with its registration.
        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return _source.RootAssets<T>();
        }

        public UniTask<T> Load<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object
        {
            reference.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            if (_loads.TryGet(reference, out var running))
                return running.Add(lifetime);

            return Create(reference).Start(lifetime);
        }

        public async UniTask<IReadOnlyList<T>> Load<T>(IReadOnlyLifetime lifetime, IReadOnlyList<ContentReference<T>> references) where T : Object
        {
            // Every load starts in this frame: their fetches run together.
            var loads = new UniTask<T>[references.Count];

            for (var i = 0; i < references.Count; i++)
                loads[i] = Load(lifetime, references[i]);

            return await UniTask.WhenAll(loads);
        }

        public T LoadNow<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object
        {
            reference.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            if (_loads.TryGet(reference, out var load))
            {
                if (load.IsLoaded == false)
                    throw new InvalidOperationException($"[Content] {typeof(T).Name} {reference} is loading asynchronously, LoadNow cannot wait for it");

                load.AddHold(lifetime);
                return reference.Target;
            }

            if (_source.IsReal(reference) == false)
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
            return _source.Fetch(lifetime, reference, Report(reference));
        }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentScene scene)
        {
            scene.ThrowIfEmpty();
            return _source.Fetch(lifetime, scene, Report(scene));
        }

        public IViewableProperty<ContentProgress> Progress(ContentReference reference)
        {
            reference.ThrowIfEmpty();
            return ProgressOf(reference);
        }

        public IViewableProperty<ContentProgress> Progress(ContentScene scene)
        {
            scene.ThrowIfEmpty();
            return ProgressOf(scene);
        }

        private IProgress<long> Report(ContentScene scene)
        {
            return new ContentProgressReport(ProgressOf(scene));
        }

        private IProgress<long> Report(ContentReference reference)
        {
            return new ContentProgressReport(ProgressOf(reference));
        }

        private ContentObjectLoad<T> Create<T>(ContentReference<T> reference) where T : Object
        {
            var load = new ContentObjectLoad<T>(reference, _source, Report(reference), _loads.Remove);
            _loads.Add(reference, load);
            return load;
        }

        private ViewableProperty<ContentProgress> ProgressOf(ContentReference reference)
        {
            return _loads.Progress($"{reference.Guid}:{reference.LocalId}", () =>
            {
                var bytes = _source.Bytes(reference);
                return new ContentProgress(_source.IsReal(reference) ? bytes : 0, bytes);
            });
        }

        // A scene starts at 0 bytes: its first fetch reports the files real already.
        private ViewableProperty<ContentProgress> ProgressOf(ContentScene scene)
        {
            return _loads.Progress(scene.Guid, () => new ContentProgress(0, _source.Bytes(scene)));
        }
    }
}
