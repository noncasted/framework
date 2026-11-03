using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Internal.Tests
{
    // The audio session of the built page: the script, run on node with and without navigator.audioSession, and its place in
    // the built index.html - before the scripts of the template, so before createUnityInstance.
    public class WebAudioSessionTests
    {
        [Test]
        public void Inject_BeforeTheScriptsOfTheTemplate()
        {
            var script = File.ReadAllText(WebAudioSessionInjection.ScriptPath());
            var html = BuiltPageScripts.Inject(
                "<html>\n<head>\n<script src=\"TemplateData/web-shell.js\"></script>\n</head>\n<body><script>createUnityInstance();</script></body>\n</html>",
                WebAudioSessionInjection.Marker, script);

            var session = html.IndexOf("navigator.audioSession.type");
            Assert.Greater(session, html.IndexOf("<head>"));
            Assert.Less(session, html.IndexOf("web-shell.js"));
            Assert.Less(session, html.IndexOf("createUnityInstance();"));
            StringAssert.Contains($"<script {WebAudioSessionInjection.Marker}>", html);
        }

        [Test]
        public void Script_SetsPlayback()
        {
            Assert.AreEqual("playback", RunOnNode("{ audioSession: { type: 'auto' } }", "navigator.audioSession.type"));
        }

        [Test]
        public void Script_WithoutAudioSession_DoesNothing()
        {
            Assert.AreEqual("undefined", RunOnNode("{}", "String(navigator.audioSession)"));
        }

        private static string RunOnNode(string navigator, string result)
        {
            var script = File.ReadAllText(WebAudioSessionInjection.ScriptPath());
            var program = $"const navigator = {navigator};\n{script}\nprocess.stdout.write({result});";
            var path = Path.Combine(Path.GetTempPath(), $"audio-session-{System.Guid.NewGuid():N}.js");
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
