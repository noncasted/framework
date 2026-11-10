namespace Global.MatchMetrics
{
    // The values of one series in the buckets Start, Start + 1, ...; StartMs: the real time (IMatchMetricsTicks) when the
    // bucket Start began.
    public class MatchChunkRecord
    {
        public MatchChunkRecord(int slot, int start, double startMs, float[] values)
        {
            Slot = slot;
            Start = start;
            StartMs = startMs;
            Values = values;
        }

        public int Slot { get; }
        public int Start { get; }
        public double StartMs { get; }
        public float[] Values { get; }
    }
}
