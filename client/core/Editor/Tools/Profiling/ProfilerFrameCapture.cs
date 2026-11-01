using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Profiling;
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
    /// so frames are looked up by time - see <see cref="TryResolveRange"/>.
    /// </summary>
    [InitializeOnLoad]
    [NoAutoStaticsCleanup]
    public static class ProfilerFrameCapture
    {
        private const string EnabledKey = "Internal.Profiler.CaptureFrames";

        /// <summary>Main thread. Loading runs on it, and dumping all threads bloats the file by an order of magnitude.</summary>
        private const int MainThreadIndex = 0;

        private const double MinSampleMs = 0.1d;
        private const int MaxDepth = 32;
        private const int MaxSamplesPerFrame = 1500;
        private const int MaxFrames = 900;

        /// <summary>
        /// Overall cap for the dump. Loading takes over a thousand frames, and without it a single run
        /// with deep profiling puts a hundred megabytes into traces.
        /// </summary>
        private const int MaxTotalSamples = 150_000;

        /// <summary>How many offset measurements to keep, to take the median and not drift on an outlier.</summary>
        private const int OffsetSamples = 32;

        /// <summary>
        /// How many editor ticks to wait for the profiler to catch up with the trace tail. The last loading
        /// frames get into the buffer only after the trace has closed, and the heaviest of
        /// them are exactly the tail ones.
        /// </summary>
        private const int TailTicks = 30;

        private static readonly List<int> _offsets = new();

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

            if (_offsets.Count > OffsetSamples)
                _offsets.RemoveAt(0);
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

            return TryGetBounds(ProfilerDriver.lastFrameIndex, out _, out var endNs) && endNs >= trace.EndNs;
        }

        public static ProfilerFramesData Capture(ProfilerTraceData trace)
        {
            var frames = new ProfilerFramesData
            {
                TraceName = trace.Name,
                TraceId = trace.Id,
                FirstFrame = trace.StartFrame,
                LastFrame = trace.EndFrame,
                DeepProfiling = ProfilerDriver.deepProfiling,
                MinSampleMs = MinSampleMs,
                ThreadName = "Main Thread"
            };

            if (TryResolveRange(trace, out var first, out var last, out var frameOffset) == false)
            {
                frames.Warning = "Профайлер не писал кадры этого запуска: включи запись в окне Profiler.";
                return frames;
            }

            var baseStartMs = double.NaN;
            var samples = new List<ProfilerSampleData>();
            var captured = 0;

            for (var profilerFrame = first; profilerFrame <= last; profilerFrame++)
            {
                if (frames.Frames.Count >= MaxFrames)
                {
                    frames.Warning = $"Кадров больше {MaxFrames}, дамп обрезан.";
                    break;
                }

                if (captured >= MaxTotalSamples)
                {
                    frames.Warning = $"Сэмплов больше {MaxTotalSamples}, дамп обрезан.";
                    break;
                }

                var data = CaptureFrame(profilerFrame + frameOffset, profilerFrame, samples);

                if (data == null)
                    continue;

                if (double.IsNaN(baseStartMs))
                    baseStartMs = data.StartMs;

                data.StartMs -= baseStartMs;
                captured += data.Samples.Count;

                frames.Frames.Add(data);
            }

            frames.MissedFrames = Math.Max(0, trace.EndFrame - trace.StartFrame + 1 - frames.Frames.Count);

            // Gaps are measured by time, not by frame count: at the start and at the tail the causes
            // differ, and they are fixed differently too.
            if (frames.Frames.Count > 0 && trace.StartNs > 0L)
            {
                frames.HeadGapMs = Math.Max(0d, (frames.Frames[0].StartNs - trace.StartNs) / 1e6d);
                frames.TailGapMs = Math.Max(0d, (trace.EndNs - frames.Frames[^1].EndNs) / 1e6d);
            }

            frames.Warning = Describe(frames);

            return frames;
        }

        /// <summary>
        /// What is missing from the dump. A silently truncated dump is worse than a missing one: from it
        /// people conclude there is nothing heavy in loading, when the heavy part simply was not recorded.
        /// </summary>
        private static string Describe(ProfilerFramesData frames)
        {
            if (string.IsNullOrEmpty(frames.Warning) == false)
                return frames.Warning;

            if (frames.Frames.Count == 0)
                return "Кадров трассы в буфере профайлера не нашлось.";

            var parts = new List<string>();

            if (frames.HeadGapMs > 1d)
            {
                parts.Add($"первые {frames.HeadGapMs:F0} мс запуска не записаны — профайлер поднимается " +
                          "пару кадров; чтобы попали, запись должна быть включена ещё до входа в плей-мод");
            }

            if (frames.TailGapMs > 1d)
                parts.Add($"последние {frames.TailGapMs:F0} мс не записаны — профайлер не успел долистать хвост");

            if (frames.DeepProfiling == false)
                parts.Add("Deep Profiling выключён: в дереве только маркеры движка, без методов скриптов");

            return string.Join("; ", parts);
        }

        /// <summary>
        /// Profiler frames covered by the trace, and the offset of their numbers to engine frames.
        ///
        /// Computed by the profiler clock: its frame numbering starts at editor startup and
        /// stands still while recording is off, so the difference between it and <see cref="Time.frameCount"/>
        /// is not constant, and old frames from a previous recording sit in the buffer as if nothing happened.
        /// Time, however, is the same on both sides, and binding by it is exact.
        /// </summary>
        private static bool TryResolveRange(ProfilerTraceData trace, out int first, out int last, out int frameOffset)
        {
            first = 0;
            last = -1;
            frameOffset = 0;

            var oldest = ProfilerDriver.firstFrameIndex;
            var newest = ProfilerDriver.lastFrameIndex;

            if (newest < oldest)
                return false;

            if (trace.StartNs <= 0L || trace.EndNs <= trace.StartNs)
                return TryResolveByFrameCount(trace, oldest, newest, out first, out last, out frameOffset);

            for (var frame = newest; frame >= oldest; frame--)
            {
                if (TryGetBounds(frame, out var startNs, out var endNs) == false)
                    continue;

                // The frame is entirely after the trace: its end has not been reached yet.
                if (startNs > trace.EndNs)
                    continue;

                if (last < 0)
                    last = frame;

                if (endNs < trace.StartNs)
                    break;

                first = frame;

                // The frame in which the trace started is its first engine frame.
                if (startNs <= trace.StartNs)
                {
                    frameOffset = trace.StartFrame - frame;
                    return true;
                }
            }

            if (last < 0)
                return false;

            // The trace start has already been evicted from the buffer - then anchor by the end.
            frameOffset = trace.EndFrame - last;

            return true;
        }

        /// <summary>
        /// Fallback for traces without the profiler clock: from a build or captured before
        /// time started being recorded. The offset here is approximate - the profiler callback arrives
        /// a frame or two late - so it does not always converge.
        /// </summary>
        private static bool TryResolveByFrameCount(
            ProfilerTraceData trace,
            int oldest,
            int newest,
            out int first,
            out int last,
            out int frameOffset)
        {
            first = 0;
            last = -1;
            frameOffset = 0;

            if (TryGetOffset(out var offset) == false)
                return false;

            first = Math.Max(oldest, trace.StartFrame + offset);
            last = Math.Min(newest, trace.EndFrame + offset);
            frameOffset = -offset;

            return last >= first;
        }

        private static bool TryGetBounds(int profilerFrame, out long startNs, out long endNs)
        {
            startNs = 0L;
            endNs = 0L;

            RawFrameDataView view = null;

            try
            {
                view = ProfilerDriver.GetRawFrameDataView(profilerFrame, MainThreadIndex);

                if (view == null || view.valid == false)
                    return false;

                startNs = (long)view.frameStartTimeNs;
                endNs = startNs + (long)view.frameTimeNs;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                view?.Dispose();
            }
        }

        private static ProfilerFrameData CaptureFrame(int frame, int profilerFrame, List<ProfilerSampleData> samples)
        {
            HierarchyFrameDataView view = null;

            try
            {
                view = ProfilerDriver.GetHierarchyFrameDataView(
                    profilerFrame,
                    MainThreadIndex,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnTotalTime,
                    false);

                if (view == null || view.valid == false)
                    return null;

                var root = view.GetRootItemID();

                var data = new ProfilerFrameData
                {
                    Frame = frame,
                    ProfilerFrame = profilerFrame,
                    StartMs = view.frameStartTimeMs,
                    StartNs = (long)view.frameStartTimeNs,
                    EndNs = (long)(view.frameStartTimeNs + view.frameTimeNs),
                    DurationMs = view.frameTimeMs,
                    GcAlloc = (long)view.GetItemColumnDataAsDouble(root, HierarchyFrameDataView.columnGcMemory)
                };

                samples.Clear();
                Append(view, root, -1, samples, out var truncated);

                data.Samples = new List<ProfilerSampleData>(samples);
                data.Truncated = truncated;

                return data;
            }
            finally
            {
                view?.Dispose();
            }
        }

        /// <summary>
        /// Depth-first traversal of the frame tree. The root is not written to the dump: it is a service node
        /// "whole frame", its numbers are already in <see cref="ProfilerFrameData.DurationMs"/>.
        /// </summary>
        private static void Append(
            HierarchyFrameDataView view,
            int id,
            int depth,
            List<ProfilerSampleData> samples,
            out bool truncated)
        {
            truncated = false;

            if (depth >= 0)
            {
                if (samples.Count >= MaxSamplesPerFrame)
                {
                    truncated = true;
                    return;
                }

                samples.Add(new ProfilerSampleData
                {
                    Depth = depth,
                    Name = view.GetItemName(id),
                    TotalMs = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnTotalTime),
                    SelfMs = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnSelfTime),
                    Calls = (int)view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls),
                    GcAlloc = (long)view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnGcMemory)
                });
            }

            if (depth + 1 >= MaxDepth || view.HasItemChildren(id) == false)
                return;

            // A children list cannot be shared by the whole traversal: recursion would overwrite it on the very first
            // child, so each level keeps its own copy.
            var children = new List<int>();
            view.GetItemChildren(id, children);

            foreach (var child in children)
            {
                // Cutoff threshold: without it a single frame with deep profiling is tens of thousands
                // of samples like "List`1.get_Count() 0.00ms", in which nothing can be seen.
                if (view.GetItemColumnDataAsDouble(child, HierarchyFrameDataView.columnTotalTime) < MinSampleMs)
                    continue;

                Append(view, child, depth + 1, samples, out var childTruncated);
                truncated |= childTruncated;
            }
        }

        /// <summary>
        /// Offset between <see cref="Time.frameCount"/> and the profiler frame counter. Takes the median
        /// of accumulated measurements: a single measurement can be off by a frame if the profiler callback
        /// arrived after the engine had already started the next frame.
        /// </summary>
        private static bool TryGetOffset(out int offset)
        {
            offset = 0;

            if (_offsets.Count == 0)
                return false;

            var sorted = new List<int>(_offsets);
            sorted.Sort();

            offset = sorted[sorted.Count / 2];

            return true;
        }
    }
}