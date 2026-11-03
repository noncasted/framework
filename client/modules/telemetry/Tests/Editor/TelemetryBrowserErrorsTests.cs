using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The side of the page on node: the jslib with the browser replaced by stubs.
    public class TelemetryBrowserErrorsTests
    {
        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-errors.test.js");
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
    }
}
