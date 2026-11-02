using System;
using System.Collections.Generic;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Internal
{
    // Reads the samples of the frames of a trace out of the profiler buffer.
    internal static class ProfilerFrameDump
    {
        private const double MinSampleMs = 0.1d;
        private const int MaxDepth = 32;
        private const int MaxSamplesPerFrame = 1500;
        private const int MaxFrames = 900;

        /// <summary>
        /// Overall cap for the dump. Loading takes over a thousand frames, and without it a single run
        /// with deep profiling puts a hundred megabytes into traces.
        /// </summary>
        private const int MaxTotalSamples = 150_000;

        public static ProfilerFramesData Capture(ProfilerTraceData trace, ProfilerFrameOffsets offsets)
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

            if (ProfilerFrameRange.TryResolveRange(trace, offsets, out var first, out var last, out var frameOffset) == false)
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

        private static ProfilerFrameData CaptureFrame(int frame, int profilerFrame, List<ProfilerSampleData> samples)
        {
            HierarchyFrameDataView view = null;

            try
            {
                view = ProfilerDriver.GetHierarchyFrameDataView(
                    profilerFrame,
                    ProfilerFrameRange.MainThreadIndex,
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
    }
}
