using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Batchmode entry point for CI/deploy scripts:
    ///     Unity -quit -batchmode -projectPath client -executeMethod Internal.WebGlBuild.Build
    ///     Output goes to client/build (or -buildOutput), with the settings of the active build profile.
    ///     The project <see cref="IPlayerBuildStep" />s run around the build (<see cref="PlayerBuildSteps" />).
    /// </summary>
    public static class WebGlBuild
    {
        private const string DefaultOutput = "build";

        [MenuItem(FrameworkMenu.Build + "WebGL", priority = FrameworkMenu.GroupPriority)]
        public static void Build()
        {
            var output = GetArgument("-buildOutput") ?? DefaultOutput;

            if (Application.isBatchMode == false)
            {
                BuildChecked(output);
                return;
            }

            // -quit alone swallows exceptions and still exits 0, so the deploy
            // script would happily package a stale build. Exit explicitly: 1 for a failed
            // build and for any exception (a project step included), only after every AfterBuild.
            try
            {
                BuildChecked(output);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void BuildChecked(string output)
        {
            var report = PlayerBuildSteps.Run(BuildTarget.WebGL, () => BuildPlayer(output));
            var summary = report.summary;

            Debug.Log($"WebGL build {summary.result}: {summary.totalSize} bytes -> {output}");

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"WebGL build failed: {summary.result}");
        }

        private static BuildReport BuildPlayer(string output)
        {
            // In batchmode the deferred recalculation of shared assets does not finish before the build.
            // Runs after the project steps: the assets they add are synced too.
            SharedAddressablesSync.Sync();

            var scenes = EditorBuildSettings.scenes
                                            .Where(scene => scene.enabled)
                                            .Select(scene => scene.path)
                                            .ToArray();

            if (scenes.Length == 0)
                throw new Exception("No enabled scenes in Build Settings.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            return BuildPipeline.BuildPlayer(options);
        }

        private static string GetArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }
    }
}