using Internal;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Global.Telemetry
{
    // Puts WebTemplate~/startup-trace.js first into the <head> of the index.html of a web build (BuiltPageScripts), so the
    // template of the game finds window.startupTrace from its first script and the telemetry reads the page before the build.
    public class TelemetryStartupTraceInjection : IPostprocessBuildWithReport
    {
        public const string Marker = "data-startup-trace";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            BuiltPageScripts.InjectIntoBuild(report, Marker, ScriptPath());
        }

        public static string Inject(string html, string script)
        {
            return BuiltPageScripts.Inject(html, Marker, script);
        }

        public static string ScriptPath()
        {
            return BuiltPageScripts.ScriptPath(typeof(TelemetryStartupTraceInjection), "startup-trace.js");
        }
    }
}
