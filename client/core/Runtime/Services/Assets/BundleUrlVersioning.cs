using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Internal
{
    // The hash in the bundle name (AppendHash) is computed from the input assets, not from the bytes: after a Unity upgrade
    // the bundle is rebuilt under the same name. The WebGL loader serves .bundle from UnityCache as immutable
    // (see index.html), so the URL must change together with the content: the bundle CRC is appended to it.
    public static class BundleUrlVersioning
    {
        public static void Setup()
        {
            Addressables.InternalIdTransformFunc = Transform;
        }

        private static string Transform(IResourceLocation location)
        {
            var id = location.InternalId;

            if (location.Data is not AssetBundleRequestOptions options || options.Crc == 0)
                return id;

            // Local paths are loaded via LoadFromFile, a query breaks the path for them.
            if (id.StartsWith("http") == false)
                return id;

            return $"{id}?crc={options.Crc:x8}";
        }
    }
}
