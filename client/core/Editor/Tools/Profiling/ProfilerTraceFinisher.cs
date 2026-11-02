using UnityEditor;

namespace Internal
{
    // Play mode exited before the game finished its trace (before the menu, say): the trace is closed here, so an
    // unfinished run is saved too, whether the Startup Profiler window is open or not.
    [InitializeOnLoad]
    internal static class ProfilerTraceFinisher
    {
        static ProfilerTraceFinisher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
                return;

            if (GameProfiler.IsRunning)
                GameProfiler.Finish();
        }
    }
}
