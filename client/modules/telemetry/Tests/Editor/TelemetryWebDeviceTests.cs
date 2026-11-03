using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    public class TelemetryWebDeviceTests
    {
        private const string _full =
            "{\"browser\":\"Chrome\",\"browserMajor\":130,\"os\":\"Android\",\"osMajor\":14,\"gpu\":\"Adreno (TM) 730\"," +
            "\"cores\":8,\"memoryClass\":4,\"screenWidth\":412,\"screenHeight\":915,\"dpr\":2.625}";

        private const string _unknown =
            "{\"browser\":\"\",\"browserMajor\":-1,\"os\":\"\",\"osMajor\":-1,\"gpu\":\"\"," +
            "\"cores\":-1,\"memoryClass\":-1,\"screenWidth\":412,\"screenHeight\":915,\"dpr\":1}";

        [Test]
        public void Parse_AllFields()
        {
            var device = TelemetryWebDeviceParser.Parse(_full, "1.2.3");

            Assert.AreEqual("webgl", device.Platform);
            Assert.AreEqual("1.2.3", device.BuildVersion);
            Assert.AreEqual("Chrome", device.Browser);
            Assert.AreEqual(130, device.BrowserMajor);
            Assert.AreEqual("Android", device.Os);
            Assert.AreEqual(14, device.OsMajor);
            Assert.AreEqual("Adreno (TM) 730", device.Gpu);
            Assert.AreEqual(8, device.Cores);
            Assert.AreEqual(4d, device.MemoryClass);
            Assert.AreEqual(412, device.ScreenWidth);
            Assert.AreEqual(915, device.ScreenHeight);
            Assert.AreEqual(2.625d, device.Dpr);
        }

        [Test]
        public void Parse_NotKnown_IsNull_NotSent()
        {
            var device = TelemetryWebDeviceParser.Parse(_unknown, "1.2.3");

            Assert.IsNull(device.Browser);
            Assert.IsNull(device.BrowserMajor);
            Assert.IsNull(device.Os);
            Assert.IsNull(device.OsMajor);
            Assert.IsNull(device.Gpu);
            Assert.IsNull(device.Cores);
            Assert.IsNull(device.MemoryClass);
        }

        // The form of the page's script is checked on the page's side: node runs TelemetryWeb.jslib against fixtures of
        // Chrome Android, Telegram WebView, Safari iOS, Firefox and asserts that the full user agent is not handed over.
        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-web.test.js");
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
