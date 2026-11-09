using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Builds the content directory of the project (<see cref="FrameworkSettings.ContentDirectory" />) together with
    ///     every player build: the roots (<see cref="IContentBuildStep" />), the directory in Library/Content/&lt;name&gt;,
    ///     the check of its lazy references, its archive in the order of first use (<see cref="ContentArchiveWriter" />),
    ///     its index and the log of its sizes. The player gets the archive and the index (<see cref="ContentShipment" />)
    ///     as StreamingAssets/Content/&lt;name&gt; (<see cref="ContentBuildPlayerProcessor" />) and the stamp
    ///     (<see cref="ContentStamp" />) as a json TextAsset in Resources; a preloaded archive of a WebGL build goes into
    ///     the .data file. The temporary files are removed after the build, also a failed one.
    ///     <para>
    ///         A build through <see cref="PlayerBuildSteps" /> prepares the directory in <see cref="BeforeBuild" />; any
    ///         other build (the build window, a script of a game) in <see cref="ContentBuildPlayerProcessor" />.
    ///     </para>
    /// </summary>
    public sealed class ContentBuild : IPlayerBuildStep
    {
        public const string Output = "Library/Content";

        // The directory of the running player build, null between builds.
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
                throw new BuildFailedException($"[ContentBuild] The content directory {Current.Directory.Name} is already prepared for a build");

            // Left by an editor that died inside a build.
            ContentBuildFiles.Delete();

            var directory = FrameworkSettings.ContentDirectory;
            var manifest = ContentDirectoryBuilder.Build(directory, Output, out var reportDirectory);

            ContentReferenceCheck.Run(manifest);

            // Only WebGL fetches StreamingAssets: elsewhere the files are on the disk of the player already.
            var preloaded = directory.IsPreloaded && target == BuildTarget.WebGL;
            var fetched = target == BuildTarget.WebGL && preloaded == false;
            var brotli = ContentBrotli.IsShipped(target, preloaded);

            var archive = $"{ContentArchiveWriter.Output}/{manifest.Name}";
            var loads = ContentLoadOrder.Read(manifest.Name);
            var index = ContentIndexWriter.Write(manifest, ContentArchiveWriter.Write(manifest, loads, archive));
            Debug.Log($"[ContentBuild] {manifest.Name}: load order {ContentLoadOrder.PathOf(manifest.Name)}, " +
                      $"{ContentArchiveOrder.Known(manifest, loads)} of {loads.Count} keys in the directory");
            var shipped = ContentShipment.Write(index, archive, fetched, brotli);
            var stamp = new ContentStamp(index.ManifestHash, preloaded, brotli);
            ContentBuildLog.Write(manifest, index, shipped);
            ContentBuildFiles.WriteStamp(index.Name, stamp);

            if (preloaded)
                ContentBuildFiles.WritePreloadManifest(index);

            Debug.Log($"[ContentBuild] {index.Name} for {target}: StreamingAssets/{ContentPaths.Directory(index.Name)} from {shipped}" +
                      (preloaded ? $", preloaded ({ContentBuildFiles.PreloadManifest})" : string.Empty) +
                      $", stamp {ContentBuildFiles.StampAsset(index.Name)}");

            Current = new ContentBuildOutput(directory, manifest.Folder, shipped, index, stamp, reportDirectory);
            return Current;
        }

        public static void Cleanup()
        {
            Current = null;
            ContentBuildFiles.Delete();
        }
    }
}
