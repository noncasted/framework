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
        internal ContentLoader(IContentSource source, ContentLoads loads)
        {
            _source = source;
            _loads = loads;
            _scenes = new ContentSceneLoads(source, Report);
        }

        private readonly IContentSource _source;
        private readonly ContentLoads _loads;
        private readonly ContentSceneLoads _scenes;

        public string Name => _source.Name;

        // The main bundle of the build (a player) or of the project (Play Mode), held while the lifetime lives, without a
        // scope (a smoke player, a tool); a game calls AddContent.
        public static UniTask<ContentLoader> Register(IReadOnlyLifetime lifetime, ContentOptions options)
        {
            options.Validate();
            return ContentRuntime.Open(options).Register(lifetime, options.Name);
        }

        // The root assets of the bundle, loaded with its registration.
        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return _source.RootAssets<T>();
        }

        public UniTask<T> Load<T>(IReadOnlyLifetime lifetime, ContentReference<T> reference) where T : Object
        {
            reference.ThrowIfEmpty();
            lifetime.Token.ThrowIfCancellationRequested();

            if (_loads.Objects.TryGetValue(reference, out var running))
                return ((ContentObjectLoad<T>)running).Add(lifetime);

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

            if (_loads.Objects.TryGetValue(reference, out var running))
            {
                var load = (ContentObjectLoad<T>)running;

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
            return ProgressOf($"{reference.Guid}:{reference.LocalId}", () => _source.Bytes(reference), () => _source.IsReal(reference));
        }

        public IViewableProperty<ContentProgress> Progress(ContentScene scene)
        {
            scene.ThrowIfEmpty();
            return ProgressOf(scene.Guid, () => _source.Bytes(scene), () => false);
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
            var load = new ContentObjectLoad<T>(reference, _source, Report(reference), OnObjectOver);
            _loads.Objects.Add(reference, load);
            return load;
        }

        private void OnObjectOver(ContentReference reference)
        {
            _loads.Objects.Remove(reference);
        }

        // A scene starts at 0 bytes: its first fetch reports the files real already.
        private IViewableProperty<ContentProgress> ProgressOf(string key, Func<long> total, Func<bool> isReal)
        {
            if (_loads.Progress.TryGetValue(key, out var progress))
                return progress;

            var bytes = total();
            progress = new ViewableProperty<ContentProgress>(new ContentProgress(isReal() ? bytes : 0, bytes));
            _loads.Progress.Add(key, progress);
            return progress;
        }
    }
}
