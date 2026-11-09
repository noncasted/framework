using Unity.Loading;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Internal
{
    // Writes the GUID and the local id of a content reference (the keys of the ContentIndex) from its engine id: the
    // drawers after an edit, the tools and the generators that assign references in code.
    public static class ContentReferenceFields
    {
        public const string Guid = "_guid";
        public const string LocalId = "_localId";
        public const string Loadable = "_loadable";
        public const string ObjectId = "m_LoadableObjectId";
        public const string SceneId = "_id";

        public static ContentReference<T> Create<T>(T asset) where T : Object
        {
            var id = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
            var (guid, localId) = Deconstruct(id);
            return new ContentReference<T>(id, guid, localId);
        }

        public static ContentScene CreateScene(string path)
        {
            var id = LoadableSceneIdEditorUtility.CreateLoadableSceneId(path);
            return new ContentScene(id, SceneGuid(id));
        }

        // The GUID of the asset and the local id of the object, empty and 0 for an empty id.
        public static (string Guid, long LocalId) Deconstruct(LoadableObjectId id)
        {
            if (id.IsValid == false || LoadableObjectIdEditorUtility.TryDeconstructLoadableObjectId(id, out var guid, out var localId, out _) == false)
                return (string.Empty, 0);

            return (guid.ToString(), localId);
        }

        public static string SceneGuid(LoadableSceneId id)
        {
            return id.IsValid ? LoadableSceneIdEditorUtility.LoadableSceneIdToGuid(id).ToString() : string.Empty;
        }

        // A ContentReference<T> property: its GUID and local id follow its Loadable. True when they changed.
        public static bool SyncObject(SerializedProperty reference)
        {
            var id = (LoadableObjectId)reference.FindPropertyRelative($"{Loadable}.{ObjectId}").boxedValue;
            var (guid, localId) = Deconstruct(id);
            var guidProperty = reference.FindPropertyRelative(Guid);
            var localIdProperty = reference.FindPropertyRelative(LocalId);

            if (guidProperty.stringValue == guid && localIdProperty.longValue == localId)
                return false;

            guidProperty.stringValue = guid;
            localIdProperty.longValue = localId;
            return true;
        }

        // A ContentScene property: its GUID follows its id. True when it changed.
        public static bool SyncScene(SerializedProperty scene)
        {
            var guid = SceneGuid((LoadableSceneId)scene.FindPropertyRelative(SceneId).boxedValue);
            var guidProperty = scene.FindPropertyRelative(Guid);

            if (guidProperty.stringValue == guid)
                return false;

            guidProperty.stringValue = guid;
            return true;
        }

        // Assigns an asset (null clears) to a ContentReference<T> property.
        public static void AssignObject(SerializedProperty reference, Object asset)
        {
            var id = asset == null ? default : LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
            reference.FindPropertyRelative($"{Loadable}.{ObjectId}").boxedValue = id;
            SyncObject(reference);
        }

        // Assigns a scene by its path (null clears) to a ContentScene property.
        public static void AssignScene(SerializedProperty scene, string path)
        {
            var id = path == null ? default : LoadableSceneIdEditorUtility.CreateLoadableSceneId(path);
            scene.FindPropertyRelative(SceneId).boxedValue = id;
            SyncScene(scene);
        }
    }
}
