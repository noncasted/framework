using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    // Fails the build when an asset of the project is in two content directories. Each directory is self-contained: the
    // build copies into it everything its roots reach, so an asset two of them reach would ship twice and load as two
    // objects (the engine knows no link between directories). Move the shared asset under the roots of one directory.
    // The scripts and the resources of the engine (unity_builtin_extra, the default resources) are no assets of the
    // project and may be in several.
    public static class ContentDirectoryOverlap
    {
        // The GUIDs of the resources of the engine start with zeros (0000000000000000e000..., 0000000000000000f000...).
        private const string EngineGuidPrefix = "0000000000000000";

        public static void Check(IReadOnlyList<ContentManifest> manifests)
        {
            var owners = new Dictionary<string, string>();
            var shared = new SortedSet<string>();

            foreach (var manifest in manifests)
            {
                foreach (var guid in AssetGuids(manifest))
                {
                    if (owners.TryGetValue(guid, out var owner) == false)
                        owners.Add(guid, manifest.Name);
                    else
                        shared.Add($"{Describe(guid)} in {owner} and {manifest.Name}");
                }
            }

            if (shared.Count > 0)
                throw new BuildFailedException(
                    $"[ContentBuild] {shared.Count} assets are in two content directories (keep each under the roots of one):\n" + string.Join("\n", shared));
        }

        private static IEnumerable<string> AssetGuids(ContentManifest manifest)
        {
            return manifest.SerializedFiles
                .Where(file => file.IsInDirectory && file.Kind != ContentFileKind.MonoScript && file.Guid.StartsWith(EngineGuidPrefix) == false)
                .Select(file => file.Guid)
                .Distinct();
        }

        private static string Describe(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? guid : path;
        }
    }
}
