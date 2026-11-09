using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Internal
{
    // Gives every player build the folder of the content directory as StreamingAssets/Content/<name>, without a copy in
    // Assets, and the types the directory serialized, so that the linker keeps them. A build that did not come through PlayerBuildSteps (the build window, a script of a game) has no prepared
    // directory: it is prepared here and cleaned up after the build. Unity calls no callback after a failed build, so
    // that cleanup is also queued for the next update of the editor; a batchmode run that quits right away leaves the
    // files to the next build, which removes them first.
    public sealed class ContentBuildPlayerProcessor : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        private static bool _prepared;

        public override int callbackOrder => 0;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            var output = ContentBuild.Current;

            if (output == null)
            {
                output = ContentBuild.Prepare(context.BuildPlayerOptions.target);
                _prepared = true;
                EditorApplication.delayCall += CleanupPrepared;
            }

            context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(output.Shipped), ContentPaths.Directory(output.Index.Name));

            // The types only the directory uses (a collider of a level, a component of a prefab) stay in the player.
            context.AddPreviousBuildReportDirectory(output.ReportDirectory);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            CleanupPrepared();
        }

        private static void CleanupPrepared()
        {
            if (_prepared == false)
                return;

            _prepared = false;
            ContentBuild.Cleanup();
        }
    }
}
