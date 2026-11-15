using System;
using UnityEngine;

namespace Internal
{
    // A lazy target of the catalog: the object of an asset (its GUID and local id) and its bundle. The asset is in the
    // bundle under its GUID; SubName is the name of a sub-asset (a sprite of a texture), empty for the main object or a
    // component of a prefab.
    [Serializable]
    public sealed class ContentCatalogObject
    {
        public ContentCatalogObject(string guid, long localId, string bundle, string subName)
        {
            _guid = guid;
            _localId = localId;
            _bundle = bundle;
            _subName = subName ?? string.Empty;
        }

        [SerializeField] private string _guid;
        [SerializeField] private long _localId;
        [SerializeField] private string _bundle;
        [SerializeField] private string _subName;

        public string Guid => _guid;
        public long LocalId => _localId;
        public string Bundle => _bundle;
        public string SubName => _subName;
        public bool IsSubAsset => string.IsNullOrEmpty(_subName) == false;
    }
}
