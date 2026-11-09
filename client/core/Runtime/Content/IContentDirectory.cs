using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    // A content directory the loads of a game go through: its roots, and the files of an object or a scene made real
    // before the engine loads it. A built and registered directory in a player (ContentDirectory), the assets of the
    // project in Play Mode of the editor (EditorContentDirectory).
    internal interface IContentDirectory
    {
        string Name { get; }

        IReadOnlyList<T> RootAssets<T>() where T : Object;

        // The object: the GUID of its asset and its local id (0 for the whole asset). Progress: bytes real so far.
        UniTask FetchObject(IReadOnlyLifetime lifetime, string guid, long localId, IProgress<long> progress);

        UniTask FetchScene(IReadOnlyLifetime lifetime, string guid, IProgress<long> progress);
    }
}
