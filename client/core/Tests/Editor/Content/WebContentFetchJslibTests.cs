using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Internal.Tests
{
    // The Range requests of the archive bypass the HTTP cache of the browser: Chromium lets one request at a time use the
    // cache entry of a URL, so concurrent ranges of the one archive went to the server one after another (150 ms of
    // latency each on the stand, a menu 2.5x slower than Addressables). The browser cannot run in EditMode; the source
    // of the jslib is checked instead, the stand of the step measured the effect.
    public class WebContentFetchJslibTests
    {
        [Test]
        public void RangeRequest_BypassesTheHttpCache()
        {
            var source = File.ReadAllText(Path.GetFullPath(JslibPath()));

            Assert.That(source, Does.Contain("cache: 'no-store', headers: { Range: "));
        }

        // The cache entries of the builds before go per directory: a directory registered later (fetched when the menu
        // opens) keeps its cached files when the first one opens. On node: core/Tests~/web-content-fetch.test.js.
        [Test]
        public void KeepOnly_OnNode_RemovesOnlyTheEntriesOfItsDirectory()
        {
            // <core>/Runtime/Content/Plugins/WebContentFetch.jslib
            var core = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(JslibPath())), "..", "..", ".."));
            var script = Path.Combine(core, "Tests~", "web-content-fetch.test.js");
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

        private static string JslibPath()
        {
            return AssetDatabase.FindAssets("WebContentFetch")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Single(path => path.EndsWith("/WebContentFetch.jslib"));
        }
    }
}
