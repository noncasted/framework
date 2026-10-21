using UnityEditor;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Without focus, an editor with Run In Background off runs Play at about 10 fps.
    /// For multiplayer via Multiplayer Play Mode one window is always unfocused, so in Play
    /// the game runs in the background. Builds are not touched: the player setting stays as is.
    /// Without a limit an unfocused window produces hundreds of frames, so frames are capped without focus.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeBackgroundRunner
    {
        private const int BackgroundFrameRate = 60;
        private const int Unlimited = -1;

        static PlayModeBackgroundRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            Application.runInBackground = true;
            OnFocusChanged(EditorApplication.isFocused);
        }

        private static void OnFocusChanged(bool isFocused)
        {
            if (EditorApplication.isPlaying == false)
                return;

            Application.targetFrameRate = isFocused == true ? Unlimited : BackgroundFrameRate;
        }
    }
}
