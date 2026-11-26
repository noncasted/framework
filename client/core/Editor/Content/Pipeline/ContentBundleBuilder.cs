using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.Build.Pipeline;
using UnityEngine;

namespace Internal
{
    // Builds the laid out bundles with the Scriptable Build Pipeline (LZ4 by chunks: a bundle is read from its file as it is
    // loaded) into a folder of the target; its cache (Library/BuildCache) builds again only what changed. The assets of a
    // bundle are in it under their GUIDs (the keys of the catalog), its scenes in a bundle of their own under their paths.
    // Without type trees for a player (about a third of the bytes: safe because the bundles are built with every player, so
    // both serialize the same types); the editor loads none without them, so the tests keep them. Not BuildPipeline.BuildAssetBundles:
    // it writes the type trees into every scene bundle whatever the options, and the player then checks each type against them and
    // compiles that code of the wasm on the first scene (+100 ms on the Menu scene of the autobattler on a phone).
    public static class ContentBundleBuilder
    {
        private const string ScenesSuffix = "__scenes";

        private static readonly ILog _log = Logs.Of(typeof(ContentBundleBuilder));

        public static ContentBuiltContent Build(IReadOnlyList<ContentBundlePlan> plans, BuildTarget target, string output, bool isTypeTree)
        {
            ContentUnsavedScenes.ThrowIfDirty();
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

            var group = BuildPipeline.GetBuildTargetGroup(target);
            var parameters = new BundleBuildParameters(target, group, output) { BundleCompression = BuildCompression.LZ4 };

            if (isTypeTree == false)
                parameters.ContentBuildFlags |= ContentBuildFlags.DisableWriteTypeTree;

            var started = DateTime.Now;
            ReturnCode code;
            IBundleBuildResults results;

            using (ContentBuildTargetSelection.Of(target, group))
                code = ContentPipeline.BuildAssetBundles(parameters, new BundleBuildContent(builds), out results);

            if (code < ReturnCode.Success)
                throw new BuildFailedException($"[ContentBuild] The content bundles were not built into {output}: {code} (the reason is in the log above)");

            _log.Info("{Bundles} asset bundles for {Target} built into {Output} in {Seconds} s", builds.Count, target, output,
                Math.Round((DateTime.Now - started).TotalSeconds, 1));

            var owners = plans.SelectMany(plan => new[] { (AssetsName(plan), plan.Name), (ScenesName(plan), plan.Name) }).ToDictionary(entry => entry.Item1, entry => entry.Item2);
            var bundles = plans.Select(plan => Built(plan, results.BundleInfos, output, owners)).ToArray();
            return new ContentBuiltContent(bundles, SerializedTypes(results));
        }

        // The serialized classes of the objects written into the bundles ([Serializable] fields, [SerializeReference] instances):
        // the linker keeps them only from a link.xml (ContentLinkXml), Unity's SerializedTypes.xml covers the player data only.
        private static IReadOnlyList<ContentSerializedType> SerializedTypes(IBundleBuildResults results)
        {
            return results.WriteResults.Values
                .SelectMany(result => result.includedSerializeReferenceFQN)
                .Select(ContentSerializedType.Parse)
                .Distinct()
                .ToArray();
        }

        private static ContentBuiltBundle Built(ContentBundlePlan plan, IReadOnlyDictionary<string, BundleDetails> infos, string output, IReadOnlyDictionary<string, string> owners)
        {
            var files = new List<ContentBuiltFile>();
            var dependencies = new SortedSet<string>(StringComparer.Ordinal);

            if (plan.Assets.Count > 0)
                files.Add(File(plan, AssetsName(plan), plan.Name, false));

            if (plan.Scenes.Count > 0)
                files.Add(File(plan, ScenesName(plan), $"{plan.Name}.scenes", true));

            foreach (var name in new[] { AssetsName(plan), ScenesName(plan) }.Where(infos.ContainsKey))
            {
                foreach (var dependency in AllDependencies(infos, name))
                {
                    if (owners[dependency] != plan.Name)
                        dependencies.Add(owners[dependency]);
                }
            }

            return new ContentBuiltBundle(plan, files, dependencies.ToArray());

            ContentBuiltFile File(ContentBundlePlan owner, string unityName, string shippedName, bool isScenes)
            {
                var path = Path.Combine(output, unityName);
                var hash = infos[unityName].Hash;
                return new ContentBuiltFile(path, $"{shippedName}.{hash}.bundle", new FileInfo(path).Length, isScenes);
            }
        }

        // The bundles the objects of a bundle reach, also through other bundles: the catalog lists them flat.
        private static IReadOnlyCollection<string> AllDependencies(IReadOnlyDictionary<string, BundleDetails> infos, string name)
        {
            var all = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(infos[name].Dependencies);

            while (pending.Count > 0)
            {
                var dependency = pending.Pop();

                if (dependency == name || all.Add(dependency) == false)
                    continue;

                foreach (var next in infos[dependency].Dependencies)
                    pending.Push(next);
            }

            return all;
        }

        // Lowercase, as BuildPipeline.BuildAssetBundles named them.
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
