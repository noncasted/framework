using System;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Internal
{
    // Finds the frames of the profiler buffer covered by a trace.
    internal static class ProfilerFrameRange
    {
        /// <summary>Main thread. Loading runs on it, and dumping all threads bloats the file by an order of magnitude.</summary>
        public const int MainThreadIndex = 0;

        /// <summary>
        /// Profiler frames covered by the trace, and the offset of their numbers to engine frames.
        ///
        /// Computed by the profiler clock: its frame numbering starts at editor startup and
        /// stands still while recording is off, so the difference between it and <see cref="Time.frameCount"/>
        /// is not constant, and old frames from a previous recording sit in the buffer as if nothing happened.
        /// Time, however, is the same on both sides, and binding by it is exact.
        /// </summary>
        public static bool TryResolveRange(
            ProfilerTraceData trace,
            ProfilerFrameOffsets offsets,
            out int first,
            out int last,
            out int frameOffset)
        {
            first = 0;
            last = -1;
            frameOffset = 0;

            var oldest = ProfilerDriver.firstFrameIndex;
            var newest = ProfilerDriver.lastFrameIndex;

            if (newest < oldest)
                return false;

            if (trace.StartNs <= 0L || trace.EndNs <= trace.StartNs)
                return TryResolveByFrameCount(trace, offsets, oldest, newest, out first, out last, out frameOffset);

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
            ProfilerFrameOffsets offsets,
            int oldest,
            int newest,
            out int first,
            out int last,
            out int frameOffset)
        {
            first = 0;
            last = -1;
            frameOffset = 0;

            if (offsets.TryGetOffset(out var offset) == false)
                return false;

            first = Math.Max(oldest, trace.StartFrame + offset);
            last = Math.Min(newest, trace.EndFrame + offset);
            frameOffset = -offset;

            return last >= first;
        }

        public static bool TryGetBounds(int profilerFrame, out long startNs, out long endNs)
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
    }
}
