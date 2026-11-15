using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Internal
{
    // Writes the GUID and the local id of a content reference (the keys of the ContentCatalog) and reads its target back:
    // the drawers, the tools and the generators that assign references in code.
    public static class ContentReferenceFields
    {
        public const string Guid = "_guid";
        public const string LocalId = "_localId";

        public static ContentReference<T> Create<T>(T asset) where T : Object
        {
            var (guid, localId) = KeysOf(asset);
            return new ContentReference<T>(guid, localId);
        }

        public static ContentScene CreateScene(string path)
        {
            return new ContentScene(SceneGuid(path));
        }

        // The object a ContentReference<T> property points at, null for an empty one or a missing asset.
        public static Object Target(SerializedProperty reference)
        {
            var guid = reference.FindPropertyRelative(Guid).stringValue;
            return string.IsNullOrEmpty(guid) ? null : ContentEditorAssets.Find(guid, reference.FindPropertyRelative(LocalId).longValue);
        }

        // The scene asset a ContentScene property points at, null for an empty one or a missing scene.
        public static SceneAsset TargetScene(SerializedProperty scene)
        {
            var guid = scene.FindPropertyRelative(Guid).stringValue;
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        // Assigns an asset (null clears) to a ContentReference<T> property.
        public static void AssignObject(SerializedProperty reference, Object asset)
        {
            var (guid, localId) = KeysOf(asset);
            reference.FindPropertyRelative(Guid).stringValue = guid;
            reference.FindPropertyRelative(LocalId).longValue = localId;
        }

        // Assigns a scene by its path (null clears) to a ContentScene property.
        public static void AssignScene(SerializedProperty scene, string path)
        {
            scene.FindPropertyRelative(Guid).stringValue = SceneGuid(path);
        }

        private static (string, long) KeysOf(Object asset)
        {
            if (asset == null)
                return (string.Empty, 0);

            var keys = ContentEditorAssets.Keys(asset);

            if (string.IsNullOrEmpty(keys.Guid))
                throw new ArgumentException($"[Content] {asset.name} is no asset of the project: a content reference points at an asset", nameof(asset));

            return keys;
        }

        private static string SceneGuid(string path)
        {
            if (path == null)
                return string.Empty;

            var guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);

            if (string.IsNullOrEmpty(guid) || path.EndsWith(".unity", StringComparison.Ordinal) == false)
                throw new ArgumentException($"[Content] {path} is no scene of the project", nameof(path));

            return guid;
        }
    }
}
