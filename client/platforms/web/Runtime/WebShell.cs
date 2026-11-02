using System.Runtime.InteropServices;
using Internal;

namespace Global.Web
{
    // window.webShell of the WebGL template (TemplateData/web-shell.js) through WebShell.jslib.
    public static class WebShell
    {
        // PlatformOptions.IsMobile asks web-shell.js (Unity's own check misses iPadOS, which reports itself as a Mac).
        // Called by a project whose template loads web-shell.js, before the root scope.
        public static void UseMobileCheck()
        {
            PlatformOptions.MobileCheck = WebShellIsMobile;
        }

        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool WebShellIsMobile();
#else
        private static bool WebShellIsMobile() =>
            throw Internal.JsLibStub.Missing();
#endif
    }
}
