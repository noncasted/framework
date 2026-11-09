using System;
using Unity.Loading;
using UnityEngine;

namespace Internal
{
    // A scene of the content directory, loaded additively through IContentLoader.LoadScene on a lifetime. Keeps the GUID
    // of the scene next to its id, the key of the ContentIndex, written by the editor (ContentSceneDrawer,
    // ContentReferenceFields). Works only from the assets of a content directory: the build of a player clears it in a
    // scene of the player.
    [Serializable]
    public sealed class ContentScene
    {
        public ContentScene(LoadableSceneId id, string guid)
        {
            _id = id;
            _guid = guid;
        }

        [SerializeField] private string _guid;
        [SerializeField] private LoadableSceneId _id;

        public LoadableSceneId Id => _id;

        public string Guid => _guid;

        public bool IsEmpty => _id.IsValid == false;

        public override string ToString()
        {
            return IsEmpty ? "(empty scene)" : $"scene {_guid}";
        }

#if UNITY_EDITOR
        // The path of the scene asset, for FindInOpenScene of the mocks.
        public string EditorPath => UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.LoadableSceneIdEditorUtility.LoadableSceneIdToScene(_id));
#endif

        internal void ThrowIfEmpty()
        {
            if (IsEmpty)
                throw new ArgumentException("[Content] An empty content scene");

            if (string.IsNullOrEmpty(_guid))
                throw new ArgumentException($"[Content] The content scene {_id} has no GUID: it was not assigned in the editor");
        }
    }
}
