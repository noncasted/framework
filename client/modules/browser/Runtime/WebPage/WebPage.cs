#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using UnityEngine;
#endif

namespace Internal
{
    // The browser page around the web build: fullscreen with the keyboard lock and the question on closing the tab.
    // The browser keeps Ctrl+W, Ctrl+T, Ctrl+N to itself in a window; only a locked keyboard in fullscreen gives
    // them to the game (WebPage.jslib).
    public static class WebPage
    {
        // The page can go fullscreen: not on iPhone Safari and in the iOS Telegram WebView (no Fullscreen API for
        // the page there). Telegram Desktop has it.
        public static bool IsFullscreenSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return WebPageIsFullscreenSupported();
#else
                return true;
#endif
            }
        }

        public static void ToggleFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebPageToggleFullscreen();
#else
            Screen.fullScreen = !Screen.fullScreen;
#endif
        }

        // Outside the web there is no tab to close: nothing to ask.
        public static void SetLeaveConfirmation(bool isEnabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebPageSetLeaveConfirmation(isEnabled);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Web only: outside the web there is no page to load again.
        public static void Reload()
        {
            WebPageReload();
        }

        [DllImport("__Internal")]
        private static extern void WebPageReload();

        [DllImport("__Internal")]
        private static extern bool WebPageIsFullscreenSupported();

        [DllImport("__Internal")]
        private static extern void WebPageToggleFullscreen();

        [DllImport("__Internal")]
        private static extern void WebPageSetLeaveConfirmation(bool isEnabled);
#endif
    }
}
