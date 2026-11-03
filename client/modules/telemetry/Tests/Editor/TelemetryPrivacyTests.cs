using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The rule of the task is a white list; these calls are what would break it by accident. The comments do not count:
    // they may name what must not be read.
    public class TelemetryPrivacyTests
    {
        private static readonly string[] _forbidden =
        {
            "deviceUniqueIdentifier", "deviceName", "deviceModel", "batteryLevel", "batteryStatus", "internetReachability",
            "systemLanguage", "TimeZone", "LocationService", "userName",
            "getBattery", "navigator.connection", "navigator.language", "geolocation", "Intl.", "getTimezoneOffset",
            "document.cookie", "location.href", "localStorage",
        };

        // The one exception: the trace of the open session keeps its own key, and reads and writes nothing else.
        private const string TraceStore = "TelemetryTrace.jslib";

        private static readonly Regex _lineComment = new Regex(@"//.*$", RegexOptions.Multiline);
        private static readonly Regex _storageCall = new Regex(@"localStorage\.(\w+)\(([^,)]*)");

        [TestCaseSource(nameof(_forbidden))]
        public void Runtime_NeverReads(string call)
        {
            var root = Path.Combine(TelemetrySources.ModuleFolder, "Runtime");

            var found = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs") || path.EndsWith(".jslib"))
                .Where(path => _lineComment.Replace(File.ReadAllText(path), "").Contains(call))
                .Where(path => call != "localStorage" || Path.GetFileName(path) != TraceStore)
                .ToArray();

            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void TheTraceStore_UsesOnlyItsKey()
        {
            var path = Directory.GetFiles(Path.Combine(TelemetrySources.ModuleFolder, "Runtime"), TraceStore, SearchOption.AllDirectories).Single();
            var calls = _storageCall.Matches(_lineComment.Replace(File.ReadAllText(path), "")).Cast<Match>().ToArray();

            Assert.AreEqual(3, calls.Length);

            foreach (var match in calls)
                Assert.AreEqual("telemetryTraceKey", match.Groups[2].Value.Trim(), match.Value);
        }
    }
}
