using System;
using Unity.Loading;
using UnityEngine;

namespace Internal
{
    // A lazy reference to an object of the content directory, without its type: what a fetch and the progress of a load
    // need. Besides the engine id it keeps the GUID of the asset and the local id of the object, the key of the
    // ContentIndex: the engine has no public API to read them from a LoadableObjectId at runtime. The editor writes them
    // when the reference is assigned (ContentReferenceDrawer, ContentReferenceFields), the content build checks they match.
    // Works only from the assets of a content directory: the build of a player clears a Loadable in a scene of the player.
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

        public abstract LoadableObjectId Id { get; }

        // The GUID of the asset of the object, the key of the ContentIndex together with LocalId.
        public string Guid => _guid;

        // The local id of the object in its asset.
        public long LocalId => _localId;

        public bool IsEmpty => Id.IsValid == false;

        public override string ToString()
        {
            return IsEmpty ? "(empty)" : $"{_guid}:{_localId}";
        }

        internal void ThrowIfEmpty()
        {
            if (IsEmpty)
                throw new ArgumentException($"[Content] An empty content reference of {GetType().Name}");

            if (string.IsNullOrEmpty(_guid))
                throw new ArgumentException($"[Content] The content reference {Id} has no GUID: it was not assigned in the editor");
        }
    }
}
