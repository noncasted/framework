using System;
using UnityEngine;

namespace Internal
{
    // A scene of a content bundle, loaded additively through IContentLoader.LoadScene on a lifetime. Keeps the GUID of the
    // scene, the key of the ContentCatalog, written by the editor (ContentSceneDrawer, ContentReferenceFields).
    [Serializable]
    public sealed class ContentScene
    {
        public ContentScene(string guid)
        {
            _guid = guid;
        }

        [SerializeField] private string _guid;

        public string Guid => _guid;

        public bool IsEmpty => string.IsNullOrEmpty(_guid);

        public override string ToString()
        {
            return IsEmpty ? "(empty scene)" : $"scene {_guid}";
        }

#if UNITY_EDITOR
        // The path of the scene asset, for FindInOpenScene of the mocks.
        public string EditorPath => IsEmpty ? string.Empty : UnityEditor.AssetDatabase.GUIDToAssetPath(_guid);
#endif

        internal void ThrowIfEmpty()
        {
            if (IsEmpty)
                throw new ArgumentException("[Content] An empty content scene");
        }
    }
}
