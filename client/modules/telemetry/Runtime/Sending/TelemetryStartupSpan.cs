namespace Global.Telemetry
{
    // ParentIndex: the index of the parent in the spans of the trace, below the index of this span; -1 for a root. Times:
    // milliseconds since the start of the trace.
    public readonly struct TelemetryStartupSpan
    {
        public TelemetryStartupSpan(
            int parentIndex,
            string name,
            double startMs,
            double durationMs,
            int frames,
            bool isUnfinished,
            long allocBytes)
        {
            ParentIndex = parentIndex;
            Name = name;
            StartMs = startMs;
            DurationMs = durationMs;
            Frames = frames;
            IsUnfinished = isUnfinished;
            AllocBytes = allocBytes;
        }

        public int ParentIndex { get; }
        public string Name { get; }
        public double StartMs { get; }
        public double DurationMs { get; }
        public int Frames { get; }
        public bool IsUnfinished { get; }
        public long AllocBytes { get; }
    }
}
