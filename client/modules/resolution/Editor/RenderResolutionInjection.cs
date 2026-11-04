using Internal;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Global.Resolution
{
    // Puts WebTemplate~/render-resolution.js first into the <head> of the index.html of a web build (BuiltPageScripts): the
    // template of the game finds window.renderResolution for its config.devicePixelRatio, the setting of the game
    // (WebRenderResolution.jslib) finds it later.
    public class RenderResolutionInjection : IPostprocessBuildWithReport
    {
        public const string Marker = "data-render-resolution";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            BuiltPageScripts.InjectIntoBuild(report, Marker, ScriptPath());
        }

        public static string ScriptPath()
        {
            return BuiltPageScripts.ScriptPath(typeof(RenderResolutionInjection), "render-resolution.js");
        }
    }
}
