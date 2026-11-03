using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor.Build;

namespace Global.Telemetry.Tests
{
    // The trace of the page: the script and the reading of the jslib on node, its place in the built index.html.
    public class TelemetryPageTimelineTests
    {
        private const string Script = "window.startupTrace = {};";

        [Test]
        public void Inject_GoesFirstIntoTheHead()
        {
            var html = Inject("<!DOCTYPE html>\n<html>\n<HEAD lang=\"en\">\n<script src=\"TemplateData/web-shell.js\"></script>\n</HEAD>\n</html>");

            var script = html.IndexOf(Script);
            Assert.Greater(script, html.IndexOf("<HEAD lang=\"en\">"));
            Assert.Less(script, html.IndexOf("web-shell.js"));
            StringAssert.Contains($"<script {TelemetryStartupTraceInjection.Marker}>", html);
        }

        [Test]
        public void Inject_APageThatHasIt_StaysAsItIs()
        {
            var html = Inject("<html><head></head></html>");

            Assert.AreEqual(html, Inject(html));
        }

        [Test]
        public void Inject_APageWithoutAHead_FailsTheBuild()
        {
            Assert.Throws<BuildFailedException>(() => Inject("<html><header></header></html>"));
        }

        [Test]
        public void ScriptPath_IsTheScriptOfTheModule()
        {
            StringAssert.Contains("window.startupTrace", File.ReadAllText(TelemetryStartupTraceInjection.ScriptPath()));
        }

        [Test]
        public void ScriptAndJslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-startup-trace.test.js");
            var info = new ProcessStartInfo("node", $"--test \"{script}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, output + errors.Result);
        }

        private static string Inject(string html)
        {
            return TelemetryStartupTraceInjection.Inject(html, Script);
        }
    }
}
