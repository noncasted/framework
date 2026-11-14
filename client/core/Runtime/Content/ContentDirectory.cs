using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Loading;
using Object = UnityEngine.Object;

namespace Internal
{
    // A built content directory registered with the engine over the folder of its store. Registered once and kept while
    // its lifetime lives: UnregisterContentDirectory destroys every object loaded from it, the roots included, and a new
    // registration gives new instances.
    internal sealed class ContentDirectory : IContentDirectory
    {
        private ContentDirectory(ContentIndex index, IContentStore store, ContentDirectoryHandle handle)
        {
            _index = index;
            _store = store;
            _handle = handle;
        }

        private readonly ContentIndex _index;
        private readonly IContentStore _store;
        private readonly ContentDirectoryHandle _handle;

        public string Name => _index.Name;

        // Prepares the folder of the store (the files the registration opens) and registers it, unregistered when the
        // lifetime ends.
        public static async UniTask<ContentDirectory> Register(IReadOnlyLifetime lifetime, ContentIndex index, IContentStore store)
        {
            await store.Prepare(lifetime, index);
            lifetime.Token.ThrowIfCancellationRequested();

            ContentDirectoryHandle handle;

            using (GameProfiler.Scope($"Content: register {index.Name}"))
                handle = ContentLoadManager.RegisterContentDirectory(store.LocalPath);

            if (handle.IsValid == false)
                throw new InvalidOperationException($"[Content] The engine did not register the content directory {index.Name} at {store.LocalPath}");

            lifetime.Listen(() => ContentLoadManager.UnregisterContentDirectory(handle));
            return new ContentDirectory(index, store, handle);
        }

        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return ContentLoadManager.GetRootAssets<T>(_handle);
        }

        public UniTask FetchObject(IReadOnlyLifetime lifetime, string guid, long localId, IProgress<long> progress)
        {
            return _store.Fetch(lifetime, _index.ObjectFiles(guid, localId), progress);
        }

        public UniTask FetchScene(IReadOnlyLifetime lifetime, string guid, IProgress<long> progress)
        {
            return _store.Fetch(lifetime, _index.SceneFiles(guid), progress);
        }

        public long ObjectBytes(string guid, long localId)
        {
            return Bytes(_index.ObjectFiles(guid, localId));
        }

        public long SceneBytes(string guid)
        {
            return Bytes(_index.SceneFiles(guid));
        }

        public bool IsObjectReal(string guid, long localId)
        {
            return _store.IsReal(_index.ObjectFiles(guid, localId));
        }

        private static long Bytes(IReadOnlyList<ContentFile> files)
        {
            var bytes = 0L;

            foreach (var file in files)
                bytes += file.Size;

            return bytes;
        }
    }
}
