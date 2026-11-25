using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Flat representation of the trace for serialization: JsonUtility cannot handle recursive
    /// structures, so the tree is stored as a list with parent references.
    /// </summary>
    [Serializable]
    public class ProfilerTraceData
    {
        public string Name;

        /// <summary>Timestamp in the file name. The frame dump next to the trace is found by it.</summary>
        public string Id;

        public string StartedAt;
        public double DurationMs;
        public int Frames;

        /// <summary>First and last engine frame the trace took. Absolute.</summary>
        public int StartFrame;

        public int EndFrame;

        /// <summary>Trace bounds by the profiler clock, ns.</summary>
        public long StartNs;

        public long EndNs;

        public bool Cold;
        public List<ProfilerSpanData> Spans = new();
    }
}