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
        private ContentDirectory(IContentFiles files, IContentStore store, ContentDirectoryHandle handle)
        {
            _files = files;
            _store = store;
            _handle = handle;
        }

        private readonly IContentFiles _files;
        private readonly IContentStore _store;
        private readonly ContentDirectoryHandle _handle;

        public string Name => _files.Name;

        // Prepares the folder of the store (the files the registration opens) and registers it, unregistered when the
        // lifetime ends.
        public static async UniTask<ContentDirectory> Register(IReadOnlyLifetime lifetime, IContentFiles files, IContentStore store)
        {
            await store.Prepare(lifetime);
            lifetime.Token.ThrowIfCancellationRequested();

            ContentDirectoryHandle handle;

            using (GameProfiler.Scope($"Content: register {files.Name}"))
                handle = ContentLoadManager.RegisterContentDirectory(store.LocalPath);

            if (handle.IsValid == false)
                throw new InvalidOperationException($"[Content] The engine did not register the content directory {files.Name} at {store.LocalPath}");

            lifetime.Listen(() => ContentLoadManager.UnregisterContentDirectory(handle));
            return new ContentDirectory(files, store, handle);
        }

        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return ContentLoadManager.GetRootAssets<T>(_handle);
        }

        public UniTask FetchObject(IReadOnlyLifetime lifetime, string guid, long localId, IProgress<long> progress)
        {
            return _store.Fetch(lifetime, _files.ObjectFiles(guid, localId), progress);
        }

        public UniTask FetchScene(IReadOnlyLifetime lifetime, string guid, IProgress<long> progress)
        {
            return _store.Fetch(lifetime, _files.SceneFiles(guid), progress);
        }

        public long ObjectBytes(string guid, long localId)
        {
            return Bytes(_files.ObjectFiles(guid, localId));
        }

        public long SceneBytes(string guid)
        {
            return Bytes(_files.SceneFiles(guid));
        }

        public bool IsObjectReal(string guid, long localId)
        {
            return _store.IsReal(_files.ObjectFiles(guid, localId));
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
