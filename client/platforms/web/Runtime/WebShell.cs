using System.Runtime.InteropServices;
using Internal;
using UnityEngine;

namespace Global.Web
{
    // window.webShell of the WebGL template (TemplateData/web-shell.js) through WebShell.jslib.
    public static class WebShell
    {
        // PlatformOptions.IsMobile asks web-shell.js (Unity's own check misses iPadOS, which reports itself as a Mac).
        // Called by a project whose template loads web-shell.js, before the root scope.
        // On a mobile page the game also stops in the background (Run In Background off): one WebGL build serves desktop
        // and phones, so the player setting cannot differ, and a hidden tab or a locked phone must not keep the game
        // and its sound (the audio session of the page is 'playback') running.
        public static void UseMobileCheck()
        {
            PlatformOptions.SetMobileCheck(WebShellIsMobile);

            if (WebShellIsMobile() == true)
                Application.runInBackground = false;
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
