using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The files a load of one object of a content directory needs, as indices into ContentIndex.Files. The object is
    // the GUID of its asset and its local id; local id 0 stands for the whole asset, the engine builds it into one file
    // (a ScriptableObject, a prefab, a scene), while the objects of an imported asset (a texture, its sprites, a clip)
    // get a file each.
    [Serializable]
    public struct ContentObjectFiles
    {
        public ContentObjectFiles(string guid, long localId, int[] files)
        {
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("A content object needs the GUID of its asset", nameof(guid));

            if (files.Length == 0)
                throw new ArgumentException($"The content object {guid}:{localId} has no files", nameof(files));

            _guid = guid;
            _localId = localId;
            _files = files;
        }

        [SerializeField] private string _guid;
        [SerializeField] private long _localId;
        [SerializeField] private int[] _files;

        public string Guid => _guid;
        public long LocalId => _localId;
        public bool IsWholeAsset => _localId == 0;

        // The file of the object first, then its dependencies and the resources of each file.
        public IReadOnlyList<int> Files => _files;
    }
}
