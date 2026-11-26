using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    // Builds the content bundles of the project (every ContentBundle asset) together with every player build: the roots and
    // the bundles left out (IContentBuildStep), the shared bundles (ContentSharedSync), the steps around the layout and the
    // bundle build (IContentLayoutStep), the layout and its checks (ContentBundleLayout), the bundles in
    // Temp/ContentBundles/<target> (ContentBundleBuilder), their files under the names of the build (ContentShipment), the
    // catalog (ContentCatalogWriter), the types of their objects and data for the linker (ContentLinkXml) and the log.
    // The player gets the files as StreamingAssets/Content (ContentBuildPlayerProcessor), the catalog as a json TextAsset in
    // Resources; a preloaded bundle of a WebGL build goes into the .data file. The temporary files are removed after the
    // build, also a failed one.
    // A build through PlayerBuildSteps prepares the bundles in BeforeBuild; any other build (the build window, a script of a
    // game) in ContentBuildPlayerProcessor.
    public sealed class ContentBuild : IPlayerBuildStep
    {
        // Not under Library: BuildAssetBundles refuses it ("Building to the Library folder is not allowed").
        public const string Output = "Temp/ContentBundles";

        private static readonly ILog _log = Logs.Of<ContentBuild>();

        // The bundles of the running player build, null between builds.
        public static ContentBuildOutput Current { get; private set; }

        public void BeforeBuild(BuildTarget target)
        {
            Prepare(target);
        }

        public void AfterBuild(BuildTarget target, bool succeeded)
        {
            Cleanup();
        }

        public static ContentBuildOutput Prepare(BuildTarget target)
        {
            if (Current != null)
                throw new BuildFailedException("[ContentBuild] The content bundles are already prepared for a build");

            // Left by an editor that died inside a build.
            ContentPlayerStamp.Delete();

            var folder = $"{Output}/{target}";
            var content = Build(target, folder, isTypeTree: false);
            var bundles = content.Bundles;
            var output = new ContentBuildOutput(ContentShipment.Write(bundles, target), ContentLinkXml.Write(content), ContentCatalogWriter.Create(bundles, target), bundles);

            ContentPlayerStamp.WriteCatalog(output.Catalog);
            ContentPlayerStamp.WritePreloadManifest(output.Catalog);
            ContentBuildLog.Write(output);
            _log.Info("{Bundles} content bundles for {Target}: {Shipped}, catalog {Catalog}", bundles.Count, target,
                $"StreamingAssets/{ContentShippedPaths.Folder} from {output.Shipped}", ContentPlayerStamp.CatalogAsset);

            Current = output;
            return Current;
        }

        public static void Cleanup()
        {
            Current = null;
            ContentPlayerStamp.Delete();

            if (File.Exists(ContentLinkXml.Output))
                File.Delete(ContentLinkXml.Output);
        }

        // The steps, the shared bundles, the layout and the bundles of the project built into a folder (a player build, a test).
        public static ContentBuiltContent Build(BuildTarget target, string folder, bool isTypeTree)
        {
            var build = new ContentBuildContext(target);

            foreach (var step in FindSteps<IContentBuildStep>())
                step.BeforeContentBuild(build);

            ContentSharedSync.Run();

            var bundles = ContentBundleAssets.All();

            if (build.Excluded.Count > 0)
                _log.Info("Left out of this build: {Bundles}", string.Join(", ", build.Excluded));

            build.Resolve(ExcludedBundles(bundles, build.Excluded));

            // A step whose BeforeLayout threw gets no AfterBundles: its exception is the failure of the build.
            var started = new List<IContentLayoutStep>();

            try
            {
                foreach (var step in FindSteps<IContentLayoutStep>())
                {
                    step.BeforeLayout(build);
                    started.Add(step);
                }

                return ContentBundleBuilder.Build(ContentBundleLayout.Lay(bundles, build.Excluded).Plans, target, folder, isTypeTree);
            }
            finally
            {
                for (var i = started.Count - 1; i >= 0; i--)
                    started[i].AfterBundles(build);
            }
        }

        // The ContentBundle assets of these names.
        private static IReadOnlyList<ContentBundle> ExcludedBundles(IReadOnlyList<ContentBundle> bundles, IReadOnlyCollection<string> excluded)
        {
            var unknown = excluded.Where(name => bundles.Any(bundle => bundle.Name == name) == false).ToArray();

            if (unknown.Length > 0)
            {
                throw new BuildFailedException($"[ContentBuild] No content bundle {string.Join(", ", unknown)} to leave out of the build: " +
                                               $"the project has {string.Join(", ", bundles.Select(bundle => bundle.Name))}");
            }

            return bundles.Where(bundle => excluded.Contains(bundle.Name)).ToArray();
        }

        private static IReadOnlyList<T> FindSteps<T>()
        {
            return TypeCache.GetTypesDerivedFrom<T>()
                .Where(type => type.IsAbstract == false && type.ContainsGenericParameters == false)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => (T)Activator.CreateInstance(type))
                .ToArray();
        }
    }
}
