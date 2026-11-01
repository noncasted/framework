using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Internal
{
    public sealed class AssetCatalogMetadata
    {
        private const string UserDataKey = "assetCatalog";
        private const string LogTag = "AssetCatalogMetadata";

        public AssetCatalogMetadata()
        {
        }

        public AssetCatalogMetadata(bool included, string group)
        {
            Included = included;
            Group = group;
        }

        public bool Included { get; private set; }
        public string Group { get; private set; } = string.Empty;

        public void SetIncluded(bool included)
        {
            Included = included;
        }

        public void SetGroup(string group)
        {
            Group = group;
        }

        public static bool TryRead(AssetImporter importer, out AssetCatalogMetadata metadata)
        {
            metadata = null;

            if (AssetUserData.TryRead(importer, UserDataKey, LogTag, out var catalog) == false)
                return false;

            metadata = new AssetCatalogMetadata
            {
                Included = catalog["included"] != null && catalog.Value<bool>("included"),
                Group = catalog.Value<string>("group") ?? string.Empty
            };
            return true;
        }

        public static AssetCatalogMetadata ReadOrDefault(AssetImporter importer)
        {
            return TryRead(importer, out var metadata) ? metadata : new AssetCatalogMetadata();
        }

        public static void Write(AssetImporter importer, AssetCatalogMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var catalog = new JObject
            {
                ["included"] = metadata.Included,
                ["group"] = metadata.Group ?? string.Empty
            };

            if (AssetUserData.Write(importer, UserDataKey, LogTag, catalog))
                AssetGroupsRegistry.Instance.Invalidate();
        }
    }
}