using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Builds the content bundles of the project (every <see cref="ContentBundle" /> asset) together with every player
    ///     build: the roots and the bundles left out (<see cref="IContentBuildStep" />), the shared bundles
    ///     (<see cref="ContentSharedSync" />), the layout and its checks (<see cref="ContentBundleLayout" />),
    ///     the bundles in Temp/ContentBundles/&lt;target&gt; (<see cref="ContentBundleBuilder" />), their files under the
    ///     names of the build (<see cref="ContentShipment" />), the catalog (<see cref="ContentCatalogWriter" />), the types of their objects for the linker
    ///     (<see cref="ContentLinkXml" />) and the log.
    ///     The player gets the files as StreamingAssets/Content (<see cref="ContentBuildPlayerProcessor" />), the catalog as
    ///     a json TextAsset in Resources; a preloaded bundle of a WebGL build goes into the .data file. The temporary files
    ///     are removed after the build, also a failed one.
    ///     <para>
    ///         A build through <see cref="PlayerBuildSteps" /> prepares the bundles in <see cref="BeforeBuild" />; any other
    ///         build (the build window, a script of a game) in <see cref="ContentBuildPlayerProcessor" />.
    ///     </para>
    /// </summary>
    public sealed class ContentBuild : IPlayerBuildStep
    {
        // Not under Library: BuildAssetBundles refuses it ("Building to the Library folder is not allowed").
        public const string Output = "Temp/ContentBundles";

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
            ContentBuildFiles.Delete();

            var folder = $"{Output}/{target}";
            var bundles = Build(target, folder, isTypeTree: false);
            var output = new ContentBuildOutput(ContentShipment.Write(bundles, target), ContentLinkXml.Write(bundles), ContentCatalogWriter.Create(bundles, target), bundles);

            ContentBuildFiles.WriteCatalog(output.Catalog);
            ContentBuildFiles.WritePreloadManifest(output.Catalog);
            ContentBuildLog.Write(output);
            Debug.Log($"[ContentBuild] {bundles.Count} content bundles for {target}: StreamingAssets/{ContentCatalogPaths.Folder} from {output.Shipped}, " +
                      $"catalog {ContentBuildFiles.CatalogAsset}");

            Current = output;
            return Current;
        }

        public static void Cleanup()
        {
            Current = null;
            ContentBuildFiles.Delete();

            if (File.Exists(ContentLinkXml.Output))
                File.Delete(ContentLinkXml.Output);
        }

        // The steps, the shared bundles, the layout and the bundles of the project built into a folder (a player build, a test).
        public static IReadOnlyList<ContentBuiltBundle> Build(BuildTarget target, string folder, bool isTypeTree)
        {
            var build = new ContentBuildContext(target);

            foreach (var step in FindSteps())
                step.BeforeContentBuild(build);

            ContentSharedSync.Run();

            var bundles = ContentBundleAssets.All();

            if (build.Excluded.Count == 0)
                return ContentBundleBuilder.Build(ContentBundleLayout.Lay(bundles).Plans, target, folder, isTypeTree);

            Debug.Log($"[ContentBuild] Left out of this build: {string.Join(", ", build.Excluded)}");

            // The ContentRoot of this build has no group or scene of the bundles left out; the project gets the full one back.
            ContentRootGenerator.GenerateWithout(ExcludedRoots(bundles, build.Excluded));

            try
            {
                return ContentBundleBuilder.Build(ContentBundleLayout.Lay(bundles, build.Excluded).Plans, target, folder, isTypeTree);
            }
            finally
            {
                ContentRootGenerator.GenerateNow();
            }
        }

        // The paths of the roots of the bundles of these names.
        private static IReadOnlyCollection<string> ExcludedRoots(IReadOnlyList<ContentBundle> bundles, IReadOnlyCollection<string> excluded)
        {
            var unknown = excluded.Where(name => bundles.Any(bundle => bundle.Name == name) == false).ToArray();

            if (unknown.Length > 0)
            {
                throw new BuildFailedException($"[ContentBuild] No content bundle {string.Join(", ", unknown)} to leave out of the build: " +
                                               $"the project has {string.Join(", ", bundles.Select(bundle => bundle.Name))}");
            }

            return new HashSet<string>(bundles.Where(bundle => excluded.Contains(bundle.Name))
                .SelectMany(bundle => bundle.Roots)
                .Where(root => root != null)
                .Select(AssetDatabase.GetAssetPath));
        }

        internal static IReadOnlyList<IContentBuildStep> FindSteps()
        {
            return TypeCache.GetTypesDerivedFrom<IContentBuildStep>()
                .Where(type => type.IsAbstract == false && type.ContainsGenericParameters == false)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => (IContentBuildStep)Activator.CreateInstance(type))
                .ToArray();
        }
    }
}
