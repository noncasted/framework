using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Internal
{
    // Puts WebTemplate~/audio-session.js first into the <head> of the index.html of a web build (BuiltPageScripts): every
    // template of a game plays its sound in the silent mode of iOS without a line of its own.
    public class WebAudioSessionInjection : IPostprocessBuildWithReport
    {
        public const string Marker = "data-audio-session";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            BuiltPageScripts.InjectIntoBuild(report, Marker, ScriptPath());
        }

        public static string ScriptPath()
        {
            return BuiltPageScripts.ScriptPath(typeof(WebAudioSessionInjection), "audio-session.js");
        }
    }
}
