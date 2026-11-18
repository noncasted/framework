using System;
using UnityEngine;

namespace Internal
{
    // A lazy reference to an object of a content bundle, without its type: what a fetch and the progress of a load
    // need. It keeps the GUID of the asset and the local id of the object, the key of the ContentCatalog, written by the
    // editor when the reference is assigned (ContentReferenceDrawer, ContentReferenceFields); the content build checks the
    // target is in a bundle. The owner of a reference is loaded without its target.
    [Serializable]
    public abstract class ContentReference
    {
        protected ContentReference(string guid, long localId)
        {
            _guid = guid;
            _localId = localId;
        }

        [SerializeField] private string _guid;
        [SerializeField] private long _localId;

        // The GUID of the asset of the object, the key of the ContentCatalog together with LocalId.
        public string Guid => _guid;

        // The local id of the object in its asset.
        public long LocalId => _localId;

        public bool IsEmpty => string.IsNullOrEmpty(_guid);

        public override string ToString()
        {
            return IsEmpty ? "(empty)" : $"{_guid}:{_localId}";
        }

        internal void ThrowIfEmpty()
        {
            if (IsEmpty)
                throw new ArgumentException($"[Content] An empty content reference of {GetType().Name}");
        }
    }
}
