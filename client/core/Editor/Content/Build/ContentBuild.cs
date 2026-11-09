using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Builds the content directory of the project (<see cref="FrameworkSettings.ContentDirectory" />) together with
    ///     every player build: the roots (<see cref="IContentBuildStep" />), the directory in Library/Content/&lt;name&gt;,
    ///     the check of its lazy references, its index and the log of its sizes. The player gets the folder as
    ///     StreamingAssets/Content/&lt;name&gt; (<see cref="ContentBuildPlayerProcessor" />) and the index as a json
    ///     TextAsset in Resources; a preloaded directory of a WebGL build goes into the .data file. The temporary files are
    ///     removed after the build, also a failed one.
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
            var manifest = ContentDirectoryBuilder.Build(directory, Output);

            ContentReferenceCheck.Run(manifest);

            var index = ContentIndexWriter.Write(manifest);
            ContentBuildLog.Write(manifest, index);
            ContentBuildFiles.WriteIndex(index);

            // Only WebGL fetches StreamingAssets: elsewhere the files are on the disk of the player already.
            var preloaded = directory.IsPreloaded && target == BuildTarget.WebGL;

            if (preloaded)
                ContentBuildFiles.WritePreloadManifest(index);

            Debug.Log($"[ContentBuild] {index.Name} for {target}: StreamingAssets/{ContentPaths.Directory(index.Name)}" +
                      (preloaded ? $", preloaded ({ContentBuildFiles.PreloadManifest})" : string.Empty) +
                      $", index {ContentBuildFiles.IndexAsset(index.Name)}");

            Current = new ContentBuildOutput(directory, manifest.Folder, index);
            return Current;
        }

        public static void Cleanup()
        {
            Current = null;
            ContentBuildFiles.Delete();
        }
    }
}
