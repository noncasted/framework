using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Internal
{
    public sealed class AudioCatalogMetadata
    {
        public const float DefaultVolume = 1f;

        private const string UserDataKey = "audioCatalog";
        private const string LogTag = "AudioCatalogMetadata";

        public AudioCatalogMetadata()
        {
        }

        public AudioCatalogMetadata(bool included, string group)
        {
            Included = included;
            Group = group;
        }

        public bool Included { get; private set; }
        public string Group { get; private set; } = string.Empty;
        public float Volume { get; private set; } = DefaultVolume;

        public void SetIncluded(bool included)
        {
            Included = included;
        }

        public void SetGroup(string group)
        {
            Group = group;
        }

        public void SetVolume(float volume)
        {
            Volume = volume;
        }

        public static bool TryRead(AssetImporter importer, out AudioCatalogMetadata metadata)
        {
            metadata = null;

            if (AssetUserData.TryRead(importer, UserDataKey, LogTag, out var catalog) == false)
                return false;

            metadata = new AudioCatalogMetadata
            {
                Included = catalog["included"] != null && catalog.Value<bool>("included"),
                Group = catalog.Value<string>("group") ?? string.Empty,
                Volume = catalog["volume"] != null ? catalog.Value<float>("volume") : DefaultVolume
            };
            return true;
        }

        public static AudioCatalogMetadata ReadOrDefault(AssetImporter importer)
        {
            return TryRead(importer, out var metadata) ? metadata : new AudioCatalogMetadata();
        }

        public static void Write(AssetImporter importer, AudioCatalogMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var catalog = new JObject
            {
                ["included"] = metadata.Included,
                ["group"] = metadata.Group ?? string.Empty,
                ["volume"] = metadata.Volume
            };

            if (AssetUserData.Write(importer, UserDataKey, LogTag, catalog))
                AudioGroupsRegistry.Instance.Invalidate();
        }
    }
}
