using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Unity profiler samples for the frames covered by the trace. Stored as a separate file
    /// next to the trace: the trace itself is a dozen kilobytes, while the frame dump is heavy, and
    /// there is no point dragging it into every scope - a scope stores only its frame numbers.
    /// </summary>
    [Serializable]
    public class ProfilerFramesData
    {
        public string TraceName;
        public string TraceId;

        /// <summary>Range of engine frames that capture was attempted for.</summary>
        public int FirstFrame;

        public int LastFrame;

        /// <summary>Captured frames are sorted by number, but gaps are possible.</summary>
        public List<ProfilerFrameData> Frames = new();

        /// <summary>Frames that were no longer in the profiler ring buffer.</summary>
        public int MissedFrames;

        /// <summary>How many milliseconds of the trace did not get into the dump at the start and at the end.</summary>
        public double HeadGapMs;

        public double TailGapMs;

        /// <summary>Without deep profiling the tree has only engine markers, no script methods.</summary>
        public bool DeepProfiling;

        public string ThreadName;

        /// <summary>Cutoff threshold in ms: anything shorter did not get into the dump.</summary>
        public double MinSampleMs;

        public string Warning;
    }

    [Serializable]
    public class ProfilerFrameData
    {
        /// <summary>Engine frame, same counter as in <see cref="ProfilerSpanData.StartFrame"/>.</summary>
        public int Frame;

        /// <summary>Frame index inside the profiler: the frame is opened in the Profiler window by it.</summary>
        public int ProfilerFrame;

        /// <summary>Frame start relative to the start of the first captured frame, ms.</summary>
        public double StartMs;

        /// <summary>Frame bounds by the profiler clock, ns: they map the frame onto the trace spans.</summary>
        public long StartNs;

        public long EndNs;

        public double DurationMs;
        public long GcAlloc;

        /// <summary>Frame sample hierarchy, as a flat list with depth - like the trace spans.</summary>
        public List<ProfilerSampleData> Samples = new();

        /// <summary>The frame tree hit the sample limit and was truncated.</summary>
        public bool Truncated;
    }

    [Serializable]
    public class ProfilerSampleData
    {
        public int Depth;
        public string Name;
        public double TotalMs;
        public double SelfMs;
        public int Calls;
        public long GcAlloc;
    }
}