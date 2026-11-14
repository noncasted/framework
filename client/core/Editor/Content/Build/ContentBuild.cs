using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Builds the content directories of the project (<see cref="FrameworkSettings.ContentDirectories" />: the one of
    ///     the catalogues and the extra ones) together with every player build: the roots (<see cref="IContentBuildStep" />),
    ///     each directory in Library/Content/&lt;name&gt;, the check of its lazy references, the check that no asset is in two
    ///     of them (<see cref="ContentDirectoryOverlap" />), its archive in the order of first use
    ///     (<see cref="ContentArchiveWriter" />), its index and the log of its sizes. The player gets the archive and the
    ///     index of each (<see cref="ContentShipment" />) as StreamingAssets/Content/&lt;name&gt;
    ///     (<see cref="ContentBuildPlayerProcessor" />) and its stamp (<see cref="ContentStamp" />) as a json TextAsset in
    ///     Resources; a preloaded archive of a WebGL build goes into the .data file. The temporary files are removed after
    ///     the build, also a failed one.
    ///     <para>
    ///         A build through <see cref="PlayerBuildSteps" /> prepares the directories in <see cref="BeforeBuild" />; any
    ///         other build (the build window, a script of a game) in <see cref="ContentBuildPlayerProcessor" />.
    ///     </para>
    /// </summary>
    public sealed class ContentBuild : IPlayerBuildStep
    {
        public const string Output = "Library/Content";

        // The directories of the running player build in the order of the settings, null between builds.
        public static IReadOnlyList<ContentBuildOutput> Current { get; private set; }

        public void BeforeBuild(BuildTarget target)
        {
            Prepare(target);
        }

        public void AfterBuild(BuildTarget target, bool succeeded)
        {
            Cleanup();
        }

        public static IReadOnlyList<ContentBuildOutput> Prepare(BuildTarget target)
        {
            if (Current != null)
                throw new BuildFailedException($"[ContentBuild] The content directories {string.Join(", ", Current.Select(output => output.Directory.Name))} are already prepared for a build");

            // Left by an editor that died inside a build.
            ContentBuildFiles.Delete();

            var directories = FrameworkSettings.ContentDirectories;
            var built = new List<(ContentDirectorySettings, ContentManifest, string)>();

            foreach (var directory in directories)
            {
                var manifest = ContentDirectoryBuilder.Build(directory, Output, out var reportDirectory);
                ContentReferenceCheck.Run(manifest);
                built.Add((directory, manifest, reportDirectory));
            }

            ContentDirectoryOverlap.Check(built.Select(entry => entry.Item2).ToArray());

            var outputs = built.Select(entry => Ship(target, entry.Item1, entry.Item2, entry.Item3)).ToArray();
            var preloaded = outputs.Where(output => output.Stamp.IsPreloaded).Select(output => output.Index).ToArray();

            if (preloaded.Length > 0)
                ContentBuildFiles.WritePreloadManifest(preloaded);

            Current = outputs;
            return Current;
        }

        public static void Cleanup()
        {
            Current = null;
            ContentBuildFiles.Delete();
        }

        private static ContentBuildOutput Ship(BuildTarget target, ContentDirectorySettings directory, ContentManifest manifest, string reportDirectory)
        {
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

            if (preloaded)
                ContentBuildFiles.WriteIndex(index);

            ContentBuildFiles.WriteStamp(index.Name, stamp);

            Debug.Log($"[ContentBuild] {index.Name} for {target}: StreamingAssets/{ContentPaths.Directory(index.Name)} from {shipped}" +
                      (preloaded ? $", preloaded ({ContentBuildFiles.PreloadManifest})" : string.Empty) +
                      $", stamp {ContentBuildFiles.StampAsset(index.Name)}");

            return new ContentBuildOutput(directory, manifest.Folder, shipped, index, stamp, reportDirectory);
        }
    }
}
