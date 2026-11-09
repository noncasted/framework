using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    // The content directory of Play Mode while the load order is recorded: every fetch of an object or a scene (the
    // step of every load before the engine) goes to the recorder, then to the directory.
    internal sealed class RecordingContentDirectory : IContentDirectory
    {
        public RecordingContentDirectory(IContentDirectory directory, ContentLoadOrderRecorder recorder)
        {
            _directory = directory;
            _recorder = recorder;
        }

        private readonly IContentDirectory _directory;
        private readonly ContentLoadOrderRecorder _recorder;

        public string Name => _directory.Name;

        public IReadOnlyList<T> RootAssets<T>() where T : Object
        {
            return _directory.RootAssets<T>();
        }

        public UniTask FetchObject(IReadOnlyLifetime lifetime, string guid, long localId, IProgress<long> progress)
        {
            _recorder.Record(ContentLoadKey.Object(guid, localId));
            return _directory.FetchObject(lifetime, guid, localId, progress);
        }

        public UniTask FetchScene(IReadOnlyLifetime lifetime, string guid, IProgress<long> progress)
        {
            _recorder.Record(ContentLoadKey.Scene(guid));
            return _directory.FetchScene(lifetime, guid, progress);
        }

        public long ObjectBytes(string guid, long localId)
        {
            return _directory.ObjectBytes(guid, localId);
        }

        public long SceneBytes(string guid)
        {
            return _directory.SceneBytes(guid);
        }

        public bool IsObjectReal(string guid, long localId)
        {
            return _directory.IsObjectReal(guid, localId);
        }
    }
}
