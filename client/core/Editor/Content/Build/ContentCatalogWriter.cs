using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The catalog of the built bundles for a target: their files, dependencies and roots, the lazy targets and the scenes
    // each holds, and how a WebGL player gets them (preloaded, gzip).
    public static class ContentCatalogWriter
    {
        public static ContentCatalog Create(IReadOnlyList<ContentBuiltBundle> bundles, BuildTarget target)
        {
            var entries = bundles.Select(bundle => new ContentCatalogBundleEntry(
                bundle.Name,
                bundle.Files.Select(file => new ContentCatalogFileEntry(file.Name, file.Size, file.IsScenes)).ToArray(),
                bundle.Dependencies,
                bundle.Plan.Roots,
                ContentShipment.IsPreloaded(bundle.Plan, target),
                ContentShipment.IsGzip(bundle.Plan, target))).ToArray();

            var objects = bundles.SelectMany(bundle => bundle.Plan.Objects).ToArray();
            var scenes = bundles.SelectMany(bundle => bundle.Plan.SceneEntries).ToArray();

            // Checks it the way a player reads it.
            return ContentCatalog.FromJson(new ContentCatalog(entries, objects, scenes).ToJson());
        }
    }
}
