using System.Collections.Generic;

namespace Internal
{
    // What ContentBundleLayout puts into one bundle: its assets under their GUIDs (the roots, the lazy targets it owns and
    // the assets its assets and its scenes both need), its scenes, the GUIDs of its roots and the lazy targets it owns, for
    // the catalog. Whatever else its assets reference directly the engine copies in as it builds.
    public sealed class ContentBundlePlan
    {
        public ContentBundlePlan(ContentBundle bundle)
        {
            Bundle = bundle;
            Name = bundle.Name;
            IsPreloaded = bundle.IsPreloaded;
            IsGzip = bundle.IsGzip;
        }

        // The asset, for the layout only: the bundle build unloads the assets it does not use, the plan keeps what it read.
        internal ContentBundle Bundle { get; }

        public string Name { get; }
        public bool IsPreloaded { get; }
        public bool IsGzip { get; }

        public List<string> Assets { get; } = new();
        public List<string> Scenes { get; } = new();
        public List<string> Roots { get; } = new();
        public List<ContentCatalogObject> Objects { get; } = new();
        public List<ContentCatalogScene> SceneEntries { get; } = new();

        // The assets the engine copies into the bundle (no other bundle holds them), with the asset that needs each first.
        public Dictionary<string, string> Implicit { get; } = new();
    }
}
