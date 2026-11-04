#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

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

        // The text pasted since the last call (Ctrl+V / Cmd+V), null when nothing was pasted. Called every frame
        // while a field takes a paste; a field drops a paste left from before by a call when it opens.
        public static string TakePaste()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The player never reads the browser clipboard: the page keeps the text of its paste event.
            return WebClipboardTakePaste();
#else
            var isModifier = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            if (isModifier == false || Input.GetKeyDown(KeyCode.V) == false)
                return null;

            return GUIUtility.systemCopyBuffer;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebClipboardCopy(string text);

        [DllImport("__Internal")]
        private static extern string WebClipboardTakePaste();
#endif
    }
}
