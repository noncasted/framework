using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // What the content build gives the player about its bundles (Resources/Content/catalog.json, read synchronously at the
    // start): every bundle with its files and its dependencies, and where the lazy targets are - each object a reference
    // can point at (the GUID of its asset and its local id) and each scene, with its bundle. Built by ContentCatalogWriter.
    [Serializable]
    public sealed class ContentCatalog
    {
        public const int CurrentVersion = 1;

        // The path of Resources.Load<TextAsset>.
        public const string Resource = "Content/catalog";

        public ContentCatalog(IReadOnlyList<ContentCatalogBundleEntry> bundles,
            IReadOnlyList<ContentCatalogObjectEntry> objects, IReadOnlyList<ContentCatalogSceneEntry> scenes)
        {
            _version = CurrentVersion;
            _bundles = new List<ContentCatalogBundleEntry>(bundles);
            _objects = new List<ContentCatalogObjectEntry>(objects);
            _scenes = new List<ContentCatalogSceneEntry>(scenes);
        }

        [SerializeField] private int _version;
        [SerializeField] private List<ContentCatalogBundleEntry> _bundles;
        [SerializeField] private List<ContentCatalogObjectEntry> _objects;
        [SerializeField] private List<ContentCatalogSceneEntry> _scenes;

        [NonSerialized] private Dictionary<string, ContentCatalogBundleEntry> _bundlesByName;
        [NonSerialized] private Dictionary<(string, long), ContentCatalogObjectEntry> _objectsByKey;
        [NonSerialized] private Dictionary<string, ContentCatalogSceneEntry> _scenesByGuid;

        public IReadOnlyList<ContentCatalogBundleEntry> Bundles => _bundles;
        public IReadOnlyList<ContentCatalogObjectEntry> Objects => _objects;
        public IReadOnlyList<ContentCatalogSceneEntry> Scenes => _scenes;

        public static ContentCatalog Load()
        {
            var asset = Resources.Load<TextAsset>(Resource);

            if (asset == null)
                throw new InvalidOperationException($"[Content] No content catalog in Resources/{Resource}: the player was built without the content build");

            var catalog = FromJson(asset.text);
            Resources.UnloadAsset(asset);
            return catalog;
        }

        public static ContentCatalog FromJson(string json)
        {
            var catalog = JsonUtility.FromJson<ContentCatalog>(json);

            if (catalog == null || catalog._version != CurrentVersion)
                throw new InvalidOperationException($"[Content] The content catalog has the version {catalog?._version}, {CurrentVersion} expected");

            catalog.Index();
            return catalog;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public ContentCatalogBundleEntry Bundle(string name)
        {
            if (_bundlesByName.TryGetValue(name, out var bundle))
                return bundle;

            throw new KeyNotFoundException($"[Content] No content bundle {name}: the catalog has {string.Join(", ", _bundlesByName.Keys)}");
        }

        public ContentCatalogObjectEntry Object(string guid, long localId)
        {
            if (_objectsByKey.TryGetValue((guid, localId), out var entry))
                return entry;

            throw new KeyNotFoundException($"[Content] {guid}:{localId} is in no content bundle (the content build checks every lazy reference: a reference made in code?)");
        }

        public ContentCatalogSceneEntry Scene(string guid)
        {
            if (_scenesByGuid.TryGetValue(guid, out var entry))
                return entry;

            throw new KeyNotFoundException($"[Content] The scene {guid} is in no content bundle");
        }

        private void Index()
        {
            _bundlesByName = new Dictionary<string, ContentCatalogBundleEntry>(StringComparer.OrdinalIgnoreCase);
            _objectsByKey = new Dictionary<(string, long), ContentCatalogObjectEntry>();
            _scenesByGuid = new Dictionary<string, ContentCatalogSceneEntry>();

            foreach (var bundle in _bundles)
                _bundlesByName.Add(bundle.Name, bundle);

            foreach (var entry in _objects)
            {
                Bundle(entry.Bundle);
                _objectsByKey.Add((entry.Guid, entry.LocalId), entry);
            }

            foreach (var entry in _scenes)
            {
                Bundle(entry.Bundle);
                _scenesByGuid.Add(entry.Guid, entry);
            }

            foreach (var bundle in _bundles)
            {
                foreach (var dependency in bundle.Dependencies)
                    Bundle(dependency);
            }
        }
    }
}
