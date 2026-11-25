using System;

namespace Internal
{
    [Serializable]
    public class ProfilerSpanData
    {
        public int Id;
        public int ParentId;
        public int Depth;
        public string Name;
        public double StartMs;
        public double DurationMs;
        public int Frames;

        /// <summary>Engine frames the span took.</summary>
        public int StartFrame;

        public int EndFrame;

        /// <summary>Span bounds by the profiler clock, ns: samples of its frames are fetched by them.</summary>
        public long StartNs;

        public long EndNs;

        public bool Unfinished;

        /// <summary>Managed memory the main thread allocated during the span, bytes.</summary>
        public long AllocBytes;

        public double EndMs => StartMs + DurationMs;
    }
}
