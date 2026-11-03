namespace Global.Telemetry
{
    // ParentIndex: the index of the parent in the spans of the trace, below the index of this span; -1 for a root. Times:
    // milliseconds since the start of the trace. File: a file the page loaded before the build started, its name only; null for any
    // other span. TransferBytes: what came over the network, 0 unknown.
    public readonly struct TelemetryStartupSpan
    {
        public TelemetryStartupSpan(
            int parentIndex,
            string name,
            double startMs,
            double durationMs,
            int frames,
            bool isUnfinished,
            long allocBytes,
            string file = null,
            long transferBytes = 0,
            bool isCached = false)
        {
            ParentIndex = parentIndex;
            Name = name;
            StartMs = startMs;
            DurationMs = durationMs;
            Frames = frames;
            IsUnfinished = isUnfinished;
            AllocBytes = allocBytes;
            File = file;
            TransferBytes = transferBytes;
            IsCached = isCached;
        }

        public int ParentIndex { get; }
        public string Name { get; }
        public double StartMs { get; }
        public double DurationMs { get; }
        public int Frames { get; }
        public bool IsUnfinished { get; }
        public long AllocBytes { get; }
        public string File { get; }
        public long TransferBytes { get; }
        public bool IsCached { get; }
    }
}
