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

        private static readonly Regex _lineComment = new Regex(@"//.*$", RegexOptions.Multiline);

        [TestCaseSource(nameof(_forbidden))]
        public void Runtime_NeverReads(string call)
        {
            var root = Path.Combine(TelemetrySources.ModuleFolder, "Runtime");

            var found = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs") || path.EndsWith(".jslib"))
                .Where(path => _lineComment.Replace(File.ReadAllText(path), "").Contains(call))
                .ToArray();

            CollectionAssert.IsEmpty(found);
        }
    }
}
