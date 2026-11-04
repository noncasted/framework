using System.Diagnostics;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace Global.Resolution.Tests
{
    // render-resolution.js on node, over a made-up page: the render pixel ratio of a phone by the mode, the platform and the
    // address, the mode kept in the local storage, the running engine that follows the setting; and its place in the built
    // index.html, before the scripts of the template.
    public class RenderResolutionScriptTests
    {
        private const string Android = "Mozilla/5.0 (Linux; Android 14; Pixel 8) Mobile";
        private const string IPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_2 like Mac OS X) Mobile";
        private const string IPad = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)";

        [Test]
        public void Inject_BeforeTheScriptsOfTheTemplate()
        {
            var html = Internal.BuiltPageScripts.Inject(
                "<html>\n<head>\n<script src=\"TemplateData/web-shell.js\"></script>\n</head>\n<body><script>createUnityInstance();</script></body>\n</html>",
                RenderResolutionInjection.Marker, File.ReadAllText(RenderResolutionInjection.ScriptPath()));

            var script = html.IndexOf("window.renderResolution =");
            Assert.Greater(script, html.IndexOf("<head>"));
            Assert.Less(script, html.IndexOf("web-shell.js"));
            StringAssert.Contains($"<script {RenderResolutionInjection.Marker}>", html);
        }

        // A 2.75x Android of 895x393 CSS pixels: 2460x1080 high, 820x360 low (k 3).
        [TestCase(Android, null, "", 1d, 2.75d)]
        [TestCase(Android, "high", "", 1d, 2.75d)]
        [TestCase(Android, "low", "", 1d, 2.75d / 3d)]
        [TestCase(IPhone, null, "", 1d, 2.75d / 3d)]
        [TestCase(IPad, null, "", 1d, 2.75d / 3d)]
        [TestCase(IPhone, "high", "", 1d, 2.75d)]
        [TestCase(IPhone, null, "?dpr=native", 1d, 2.75d)]
        [TestCase(Android, null, "?dpr=2", 1d, 2.75d / 2d)]
        [TestCase(Android, "low", "", 2d, 2.75d / 2d)]
        public void PixelRatio_OfAPhone(string userAgent, string stored, string search, double maxRatio, double expected)
        {
            var ratio = Run(userAgent, stored, search, $"renderResolution.pixelRatio(canvas, {Number(maxRatio)})");

            Assert.That(double.Parse(ratio, CultureInfo.InvariantCulture), Is.EqualTo(expected).Within(1e-9));
        }

        // The short side of the visible canvas limits k: a canvas of 300 CSS pixels at 2.75 is 825 device pixels, k 2.
        [Test]
        public void PixelRatio_KeepsTheVisibleShortSideAt360()
        {
            var ratio = Run(Android, "low", "", "renderResolution.pixelRatio({ getBoundingClientRect: () => ({ width: 895, height: 300 }) }, 1)");

            Assert.That(double.Parse(ratio, CultureInfo.InvariantCulture), Is.EqualTo(2.75d / 2d).Within(1e-9));
        }

        [Test]
        public void Set_KeepsTheModeForTheNextStart()
        {
            Assert.That(Run(Android, null, "", "(renderResolution.set('low'), storage['webShell.resolution'])"), Is.EqualTo("low"));
            Assert.That(Run(Android, "high", "", "renderResolution.mode()"), Is.EqualTo("high"));
            Assert.That(Run(Android, null, "", "renderResolution.mode()"), Is.EqualTo("auto"));
        }

        [Test]
        public void Set_AnUnknownMode_Throws()
        {
            StringAssert.Contains("must be one of", Run(Android, null, "",
                "(() => { try { renderResolution.set('medium'); return 'no error'; } catch (error) { return error.message; } })()"));
        }

        [Test]
        public void Follow_TheRunningEngineTakesTheSetting()
        {
            var ratios = Run(Android, null, "",
                "(() => { const module = { devicePixelRatio: 1 }; renderResolution.follow(module, canvas, 1); const high = module.devicePixelRatio; " +
                "renderResolution.set('low'); return high + ' ' + module.devicePixelRatio; })()");

            Assert.That(ratios, Is.EqualTo($"2.75 {Number(2.75d / 3d)}"));
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        // The page: a screen and a canvas of 895x393 CSS pixels at 2.75, a local storage that holds the stored mode.
        private static string Run(string userAgent, string stored, string search, string result)
        {
            var storage = stored == null ? "{}" : $"{{ 'webShell.resolution': '{stored}' }}";
            var page =
                $"const storage = {storage};\n" +
                $"const navigator = {{ userAgent: '{userAgent}', maxTouchPoints: {(userAgent == IPad ? 5 : 0)} }};\n" +
                "const screen = { width: 895, height: 393 };\n" +
                "const document = { addEventListener: () => {} };\n" +
                "class ResizeObserver { observe() {} }\n" +
                "const window = { devicePixelRatio: 2.75, addEventListener: () => {}, visualViewport: undefined,\n" +
                $"    location: {{ search: '{search}' }},\n" +
                "    localStorage: { getItem: (key) => key in storage ? storage[key] : null, setItem: (key, value) => { storage[key] = value; } } };\n" +
                "const canvas = { getBoundingClientRect: () => ({ width: 895, height: 393 }) };\n";
            var program = $"{page}{File.ReadAllText(RenderResolutionInjection.ScriptPath())}\n" +
                          "const renderResolution = window.renderResolution;\n" +
                          $"process.stdout.write(String({result}));";
            var path = Path.Combine(Path.GetTempPath(), $"render-resolution-{System.Guid.NewGuid():N}.js");
            File.WriteAllText(path, program);

            try
            {
                var info = new ProcessStartInfo("node", $"\"{path}\"")
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

                Assert.AreEqual(0, process.ExitCode, errors.Result);
                return output;
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
