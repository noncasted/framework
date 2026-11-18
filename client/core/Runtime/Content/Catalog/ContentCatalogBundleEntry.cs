using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The entry of a content bundle in the catalog: its files (the bundle of its assets and, when it has scenes, the
    // bundle of its scenes: the engine keeps them apart), the bundles its assets reference directly (loaded with it,
    // every one of them, so the list is closed), its roots, whether a WebGL player has it in the .data file and whether
    // a fetched one is shipped in gzip.
    [Serializable]
    public sealed class ContentCatalogBundleEntry
    {
        public ContentCatalogBundleEntry(string name, IReadOnlyList<ContentCatalogFileEntry> files,
            IReadOnlyList<string> dependencies, IReadOnlyList<string> roots, bool isPreloaded, bool isGzip)
        {
            _name = name;
            _files = new List<ContentCatalogFileEntry>(files);
            _dependencies = new List<string>(dependencies);
            _roots = new List<string>(roots);
            _isPreloaded = isPreloaded;
            _isGzip = isGzip;
        }

        [SerializeField] private string _name;
        [SerializeField] private List<ContentCatalogFileEntry> _files;
        [SerializeField] private List<string> _dependencies;
        [SerializeField] private List<string> _roots;
        [SerializeField] private bool _isPreloaded;
        [SerializeField] private bool _isGzip;

        public string Name => _name;
        public IReadOnlyList<ContentCatalogFileEntry> Files => _files;
        public IReadOnlyList<string> Dependencies => _dependencies;
        // The GUIDs of the root assets, loaded with its registration (IContentLoader.RootAssets).
        public IReadOnlyList<string> Roots => _roots;
        public bool IsPreloaded => _isPreloaded;
        public bool IsGzip => _isGzip;
    }
}
