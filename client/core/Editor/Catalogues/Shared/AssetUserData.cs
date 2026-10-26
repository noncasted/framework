using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Internal
{
    // Catalog metadata lives in importer.userData under its own root key,
    // so catalogs overwrite neither foreign keys nor data of other tools.
    public static class AssetUserData
    {
        public static bool TryRead(AssetImporter importer, string key, string logTag, out JObject payload)
        {
            payload = null;

            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            var userData = importer.userData;

            if (string.IsNullOrWhiteSpace(userData))
                return false;

            if (Parse(importer, logTag)[key] is not JObject catalog)
                return false;

            payload = catalog;
            return true;
        }

        // Returns true if userData actually changed.
        public static bool Write(AssetImporter importer, string key, string logTag, JObject payload)
        {
            if (Stage(importer, key, logTag, payload) == false)
                return false;

            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
            return true;
        }

        // Changes userData only in the importer's memory: the importer inspector sees the mismatch
        // with .meta and enables Apply/Revert; the change reaches the disk on Apply.
        public static bool Stage(AssetImporter importer, string key, string logTag, JObject payload)
        {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var root = string.IsNullOrWhiteSpace(importer.userData) ? new JObject() : Parse(importer, logTag);
            root[key] = payload;

            var nextUserData = root.ToString(Formatting.Indented);

            if (importer.userData == nextUserData)
                return false;

            importer.userData = nextUserData;
            return true;
        }

        // userData that is not a JSON object (a merge conflict, another tool's text) cannot be read as catalog
        // markup and must not be overwritten by it: the asset would silently leave its catalogue or lose that data.
        private static JObject Parse(AssetImporter importer, string logTag)
        {
            JToken parsed;

            try
            {
                parsed = JToken.Parse(importer.userData);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"[{logTag}] userData of {importer.assetPath} is not valid json. Fix its .meta.", exception);
            }

            if (parsed is not JObject root)
                throw new InvalidOperationException(
                    $"[{logTag}] userData of {importer.assetPath} is not a JSON object. Fix its .meta.");

            return root;
        }
    }
}