using System;
using System.Collections.Generic;

namespace Internal
{
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
}
