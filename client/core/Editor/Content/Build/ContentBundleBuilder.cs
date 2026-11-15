using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal
{
    // Builds the laid out bundles with BuildPipeline.BuildAssetBundles (LZ4 by chunks: a bundle is read from its file as it
    // is loaded) into a folder of the target, kept between the builds of one editor session (the engine builds again only what changed). The
    // assets of a bundle are in it under their GUIDs (the keys of the catalog), its scenes in a bundle of their own under
    // their paths. Without type trees for a player (about a third of the bytes: safe because the bundles are built with
    // every player, so both serialize the same types); the editor loads none without them, so the tests keep them.
    public static class ContentBundleBuilder
    {
        private const string ScenesSuffix = "__scenes";

        public static IReadOnlyList<ContentBuiltBundle> Build(IReadOnlyList<ContentBundlePlan> plans, BuildTarget target, string output, bool isTypeTree)
        {
            Directory.CreateDirectory(output);

            var builds = new List<AssetBundleBuild>();

            foreach (var plan in plans)
            {
                if (plan.Assets.Count > 0)
                {
                    builds.Add(new AssetBundleBuild
                    {
                        assetBundleName = AssetsName(plan),
                        assetNames = plan.Assets.ToArray(),
                        addressableNames = plan.Assets.Select(path => AssetDatabase.AssetPathToGUID(path)).ToArray()
                    });
                }

                if (plan.Scenes.Count > 0)
                    builds.Add(new AssetBundleBuild { assetBundleName = ScenesName(plan), assetNames = plan.Scenes.ToArray() });
            }

            var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode |
                          BuildAssetBundleOptions.DisableLoadAssetByFileName | BuildAssetBundleOptions.DisableLoadAssetByFileNameWithExtension;

            if (isTypeTree == false)
                options |= BuildAssetBundleOptions.DisableWriteTypeTree;

            var started = DateTime.Now;
            var manifest = BuildPipeline.BuildAssetBundles(output, builds.ToArray(), options, target);

            if (manifest == null)
                throw new BuildFailedException($"[ContentBuild] The content bundles were not built into {output} (the reason is in the log above)");

            Debug.Log($"[ContentBuild] {builds.Count} asset bundles for {target} built into {output} in {(DateTime.Now - started).TotalSeconds:0.0} s");

            var owners = plans.SelectMany(plan => new[] { (AssetsName(plan), plan.Name), (ScenesName(plan), plan.Name) }).ToDictionary(entry => entry.Item1, entry => entry.Item2);
            return plans.Select(plan => Built(plan, manifest, output, owners)).ToArray();
        }

        private static ContentBuiltBundle Built(ContentBundlePlan plan, AssetBundleManifest manifest, string output, IReadOnlyDictionary<string, string> owners)
        {
            var built = new HashSet<string>(manifest.GetAllAssetBundles());
            var files = new List<ContentBuiltFile>();
            var dependencies = new SortedSet<string>(StringComparer.Ordinal);

            if (plan.Assets.Count > 0)
                files.Add(File(plan, AssetsName(plan), plan.Name, false));

            if (plan.Scenes.Count > 0)
                files.Add(File(plan, ScenesName(plan), $"{plan.Name}.scenes", true));

            foreach (var name in new[] { AssetsName(plan), ScenesName(plan) }.Where(built.Contains))
            {
                foreach (var dependency in manifest.GetAllDependencies(name))
                {
                    if (owners[dependency] != plan.Name)
                        dependencies.Add(owners[dependency]);
                }
            }

            return new ContentBuiltBundle(plan, files, dependencies.ToArray());

            ContentBuiltFile File(ContentBundlePlan owner, string unityName, string shippedName, bool isScenes)
            {
                var path = Path.Combine(output, unityName);
                var hash = manifest.GetAssetBundleHash(unityName);
                return new ContentBuiltFile(path, $"{shippedName}.{hash}.bundle", new FileInfo(path).Length, isScenes);
            }
        }

        // The engine lowercases the names of bundles.
        private static string AssetsName(ContentBundlePlan plan)
        {
            return plan.Name.ToLowerInvariant();
        }

        private static string ScenesName(ContentBundlePlan plan)
        {
            return plan.Name.ToLowerInvariant() + ScenesSuffix;
        }
    }
}
