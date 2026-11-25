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
}