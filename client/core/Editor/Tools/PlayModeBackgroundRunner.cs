using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Without focus, an editor with Run In Background off runs Play at about 10 fps. For multiplayer via Multiplayer
    // Play Mode one window is always unfocused, so in Play the game runs in the background. Builds are not touched: the
    // player setting stays as is. Without a limit an unfocused window produces hundreds of frames, so frames are capped
    // without focus; the limit of the game (the framerate module, ?fps) comes back with the focus.
    [InitializeOnLoad]
    public static class PlayModeBackgroundRunner
    {
        private const int BackgroundFrameRate = 60;

        private static PlayModeFrameCap _cap = new(BackgroundFrameRate);

        static PlayModeBackgroundRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.focusChanged += OnFocusChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Apply(true);
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            _cap = new PlayModeFrameCap(BackgroundFrameRate);
            Application.runInBackground = true;
            Apply(EditorApplication.isFocused);
        }

        private static void OnFocusChanged(bool isFocused)
        {
            if (EditorApplication.isPlaying == false)
                return;

            Apply(isFocused);
        }

        private static void Apply(bool isFocused)
        {
            var rate = _cap.OnFocus(isFocused, Application.targetFrameRate);

            if (rate.HasValue == true)
                Application.targetFrameRate = rate.Value;
        }
    }
}
