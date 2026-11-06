using System;
using System.IO;
using Unity.Web.Stripping.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Measurement
{
    /// <summary>
    ///     Batchmode entry of tools/measure.py (one call per project):
    ///     Unity -batchmode -buildTarget WebGL -projectPath projects/&lt;version&gt;-&lt;variant&gt;
    ///     -executeMethod Measurement.MeasurementBuild.Run -buildOutput &lt;dir&gt; -measurementStrip &lt;list&gt;
    ///     -measurementRecord &lt;json&gt; -measurementMissing Ignore|LogError [-measurementProfile].
    ///     Makes the scene and its assets, activates the stripping of the variant and builds with the web profile
    ///     copied from pixel-swarm (<see cref="ProfilePath" />). With -measurementProfile the build is not stripped
    ///     but instrumented by the submodule profiling of the stripping tool: the page records which submodules the
    ///     scene calls (tools/smoke.mjs takes the report). Exit code 1 on any failure.
    /// </summary>
    public static class MeasurementBuild
    {
        public const string ProfilePath = "Assets/Measurement/Profile/Web.asset";

        public static void Run()
        {
            // -quit alone swallows exceptions and exits 0: the exit is explicit.
            try
            {
                RunChecked();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void RunChecked()
        {
            var output = Argument("-buildOutput");
            var strip = File.ReadAllLines(Argument("-measurementStrip"));
            var recordPath = Argument("-measurementRecord");

            var assets = MeasurementAssets.Create();
            GraphicsSettings.defaultRenderPipeline = assets.Pipeline;

            var scene = MeasurementScene.Build(assets);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };

            var missing = (MissingSubmoduleErrorHandlingType)Enum.Parse(typeof(MissingSubmoduleErrorHandlingType), Argument("-measurementMissing"));
            var profiling = HasArgument("-measurementProfile");
            var submodules = profiling ? MeasurementStripping.Deactivate() : MeasurementStripping.Activate(strip, missing);
            AssetDatabase.SaveAssets();

            var profile = MeasurementAssets.Load<BuildProfile>(ProfilePath);
            var started = DateTime.UtcNow;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = output,
                options = BuildOptions.None
            });

            var summary = report.summary;

            var record = new MeasurementBuildRecord
            {
                unityVersion = Application.unityVersion,
                result = summary.result.ToString(),
                buildSeconds = (DateTime.UtcNow - started).TotalSeconds,
                reportedSize = (long)summary.totalSize,
                errors = summary.totalErrors,
                warnings = summary.totalWarnings,
                strippedSubmodules = submodules,
                missingSubmodules = missing.ToString()
            };

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"WebGL build failed: {summary.result}");

            if (profiling)
                Instrument(output);

            record.profiled = profiling;
            File.WriteAllText(recordPath, JsonUtility.ToJson(record, true));
            Debug.Log($"[measurement] WebGL build {summary.result}: {summary.totalSize} bytes -> {output}");
        }

        private static void Instrument(string output)
        {
            var build = WebBuildReportList.Instance.AddOrUpdateBuild(output);

            if (!WebBuildProcessor.InstrumentBuild(build))
                throw new Exception($"The submodule profiling of {output} failed, see the log");
        }

        private static bool HasArgument(string name)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            throw new ArgumentException($"Missing command line argument {name}");
        }
    }
}
