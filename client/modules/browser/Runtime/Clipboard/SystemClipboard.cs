#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using UnityEngine;
#endif

namespace Internal
{
    public static class SystemClipboard
    {
        public static void Copy(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // In the web the player buffer never reaches the browser clipboard.
            WebClipboardCopy(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebClipboardCopy(string text);
#endif
    }
}
