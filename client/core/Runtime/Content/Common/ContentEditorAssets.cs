#if UNITY_EDITOR
using UnityEditor;
using Object = UnityEngine.Object;

namespace Internal
{
    // The object of the project a GUID and a local id name (the keys of a ContentReference): the main asset or one of its
    // sub-assets, a component of a prefab too. Null when there is no such object.
    public static class ContentEditorAssets
    {
        public static Object Find(string guid, long localId)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                return null;

            var main = AssetDatabase.LoadMainAssetAtPath(path);

            if (main != null && Is(main, guid, localId))
                return main;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset != null && Is(asset, guid, localId))
                    return asset;
            }

            return null;
        }

        // The GUID and the local id of an object of an asset, empty and 0 for none (a scene object, null).
        public static (string Guid, long LocalId) Keys(Object asset)
        {
            if (asset == null || AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId) == false)
                return (string.Empty, 0);

            return (guid, localId);
        }

        private static bool Is(Object asset, string guid, long localId)
        {
            var (assetGuid, assetLocalId) = Keys(asset);
            return assetGuid == guid && assetLocalId == localId;
        }
    }
}
#endif
