using System.Collections.Generic;
using System.Linq;

namespace Internal
{
    // The log of the built bundles: per bundle its files and bytes, how the player gets it, its dependencies, roots, lazy
    // targets and scenes, and the assets the engine copied into it; and a warning for the built-in resources a bundle
    // copied in (they come with every scene or material that references them and are better cut at the source).
    public static class ContentBuildLog
    {
        private static readonly ILog _log = Logs.Of(typeof(ContentBuildLog));

        public static void Write(ContentBuildOutput output)
        {
            foreach (var bundle in output.Bundles)
            {
                var entry = output.Catalog.Bundle(bundle.Name);
                var delivery = entry.IsPreloaded ? "preloaded" : entry.IsGzip ? "fetched in gzip" : "fetched or read in place";

                var files = string.Join(", ", bundle.Files.Select(file => $"{file.Name} {file.Size} bytes"));
                var contents = $"dependencies [{string.Join(", ", bundle.Dependencies)}], {bundle.Plan.Roots.Count} roots, " +
                               $"{bundle.Plan.Assets.Count} assets, {bundle.Plan.Scenes.Count} scenes, {bundle.Plan.Objects.Count} lazy targets, " +
                               $"{bundle.Plan.Implicit.Count} assets copied in";

                _log.Info("{Bundle}: {Files}, {Delivery}; {Contents}", bundle.Name, files, delivery, contents);

                var builtIn = BuiltIn(bundle.Plan.Implicit.Keys);

                if (builtIn.Count > 0)
                    _log.Warning("{Bundle}: built-in resources in the bundle: {Resources}", bundle.Name, string.Join(", ", builtIn));
            }
        }

        private static IReadOnlyList<string> BuiltIn(IEnumerable<string> paths)
        {
            return paths.Where(path => path.StartsWith("Resources/unity_builtin_extra") || path.StartsWith("Library/unity default resources")).ToArray();
        }
    }
}
