using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Internal.Tests
{
    // The jslib of the fetches on node (core/Tests~/web-content-fetch.test.js): the browser cannot run in EditMode.
    public class WebContentFetchJslibTests
    {
        // The cache entries of the builds before go, the files of this build and the other entries of the page stay; a gzip
        // body is unpacked.
        [Test]
        public void Jslib_OnNode()
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
