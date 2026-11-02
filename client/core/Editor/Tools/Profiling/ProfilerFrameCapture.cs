using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Pulls from the Unity profiler the samples of the frames covered by the trace and stores them
    /// next to it as a separate file. Works only in the editor: per-frame data lives
    /// in the profiler ring buffer, which the runtime cannot reach.
    ///
    /// The profiler numbers frames with its own counter that does not match <see cref="Time.frameCount"/>,
    /// so frames are looked up by time - see <see cref="ProfilerFrameRange.TryResolveRange"/>.
    /// </summary>
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class ProfilerFrameCapture
    {

        private const string EnabledKey = "Internal.Profiler.CaptureFrames";

        /// <summary>
        /// How many editor ticks to wait for the profiler to catch up with the trace tail. The last loading
        /// frames get into the buffer only after the trace has closed, and the heaviest of
        /// them are exactly the tail ones.
        /// </summary>
        private const int TailTicks = 30;

        private static readonly ProfilerFrameOffsets _offsets = new();

        private static ProfilerTraceData _pending;
        private static string _pendingPath;
        private static int _pendingTicks;

        static ProfilerFrameCapture()
        {
            ProfilerDriver.NewProfilerFrameRecorded += OnProfilerFrameRecorded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ProfilerTraceStorage.Saved += OnTraceSaved;

            StartRecording();
        }

        /// <summary>
        /// Frame capture keeps the profiler recording, and its buffer takes gigabytes, so it is off until
        /// requested. The request lives in the session: it survives a domain reload and ends with the editor.
        /// Turning it off stops the recording and releases the buffer.
        /// </summary>
        public static bool Enabled => SessionState.GetBool(EnabledKey, false);

        public static void SetEnabled(bool enabled)
        {
            SessionState.SetBool(EnabledKey, enabled);
            ProfilerDriver.enabled = enabled;

            if (enabled == false)
                ProfilerDriver.ClearAllFrames();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            _offsets.Clear();

            StartRecording();
        }

        /// <summary>
        /// While requested, recording is kept on in advance, already in Edit Mode: if it is enabled on
        /// entering Play Mode, the first frames of the run - the heaviest ones - do not get into the buffer,
        /// the profiler takes a couple of frames to start.
        /// </summary>
        private static void StartRecording()
        {
            if (Enabled && ProfilerDriver.enabled == false)
                ProfilerDriver.enabled = true;
        }

        private static void OnProfilerFrameRecorded(int connectionId, int frameIndex)
        {
            if (EditorApplication.isPlaying == false)
                return;

            _offsets.Add(frameIndex - Time.frameCount);
        }

        /// <summary>
        /// Frames cannot be captured right here: the trace closes mid-frame, and neither that frame nor
        /// a couple of previous ones have landed in the profiler buffer yet. So we wait until the profiler
        /// catches up to the end of the trace, and only then read.
        /// </summary>
        private static void OnTraceSaved(ProfilerTraceData trace)
        {
            if (Enabled == false)
                return;

            var path = ProfilerTraceStorage.FramesPath(ProfilerTraceStorage.LastPath);

            if (string.IsNullOrEmpty(path))
                return;

            _pending = trace;
            _pendingPath = path;
            _pendingTicks = 0;

            EditorApplication.update -= WaitForTail;
            EditorApplication.update += WaitForTail;
        }

        private static void WaitForTail()
        {
            _pendingTicks++;

            if (_pendingTicks < TailTicks && HasTail(_pending) == false)
                return;

            EditorApplication.update -= WaitForTail;

            var trace = _pending;
            var path = _pendingPath;

            _pending = null;
            _pendingPath = null;

            if (trace == null || string.IsNullOrEmpty(path))
                return;

            try
            {
                var frames = Capture(trace);
                ProfilerFramesStorage.Save(path, frames);

                Debug.Log($"[Profiler] Captured {frames.Frames.Count} profiler frames" +
                          (frames.MissedFrames > 0 ? $", {frames.MissedFrames} missed" : string.Empty) +
                          $" -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profiler] Failed to capture profiler frames: {e.Message}");
            }
        }

        /// <summary>The last recorded frame covered the end of the trace - nothing more to wait for.</summary>
        private static bool HasTail(ProfilerTraceData trace)
        {
            if (trace == null || trace.EndNs <= 0L)
                return false;

            return ProfilerFrameRange.TryGetBounds(ProfilerDriver.lastFrameIndex, out _, out var endNs) && endNs >= trace.EndNs;
        }

        public static ProfilerFramesData Capture(ProfilerTraceData trace)
        {
            return ProfilerFrameDump.Capture(trace, _offsets);
        }
    }
}
