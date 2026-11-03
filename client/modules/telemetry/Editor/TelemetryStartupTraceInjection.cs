using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Global.Telemetry
{
    // Puts WebTemplate~/startup-trace.js first into the <head> of the index.html of a web build, so the template of the game finds
    // window.startupTrace from its first script and the telemetry reads the page before the build. The built page is changed, never
    // the template: no file of the game is written. A page that already has it (data-startup-trace) stays as it is; a page without a
    // <head> fails the build.
    public class TelemetryStartupTraceInjection : IPostprocessBuildWithReport
    {
        public const string Marker = "data-startup-trace";

        private static readonly Regex _head = new(@"<head(\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            var page = Path.Combine(report.summary.outputPath, "index.html");
            File.WriteAllText(page, Inject(File.ReadAllText(page), File.ReadAllText(ScriptPath())));
        }

        public static string Inject(string html, string script)
        {
            if (html.Contains(Marker))
                return html;

            var head = _head.Match(html);

            if (head.Success == false)
                throw new BuildFailedException($"[Telemetry] index.html has no <head>: {nameof(TelemetryStartupTraceInjection)} has nowhere to put the startup trace");

            var tag = $"\n<script {Marker}>\n{script.Trim()}\n</script>";

            return html.Insert(head.Index + head.Length, tag);
        }

        public static string ScriptPath()
        {
            var guid = AssetDatabase.FindAssets($"{nameof(TelemetryStartupTraceInjection)} t:MonoScript").Single();
            var editor = Path.GetDirectoryName(Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid)));
            var path = Path.Combine(Path.GetDirectoryName(editor) ?? throw new InvalidOperationException(editor), "WebTemplate~", "startup-trace.js");

            if (File.Exists(path) == false)
                throw new FileNotFoundException("[Telemetry] The startup trace of the page is missing", path);

            return path;
        }
    }
}
