using System;
using System.Collections.Generic;

namespace Global.MatchMetrics
{
    // A batch of a stream as it waits for the server. Seq is given when it is sent the first time (0 before): a batch
    // dropped from a full buffer has none, so the numbers of the server have no gap.
    public class MatchMetricsBatchRecord
    {
        public MatchMetricsBatchRecord(
            List<MatchSeriesRecord> series,
            IReadOnlyList<MatchChunkRecord> chunks,
            IReadOnlyList<MatchEventRecord> events)
        {
            _series = series;
            Chunks = chunks;
            Events = events;
        }

        private readonly List<MatchSeriesRecord> _series;

        public int Seq { get; private set; }
        public IReadOnlyList<MatchSeriesRecord> Series => _series;
        public IReadOnlyList<MatchChunkRecord> Chunks { get; }
        public IReadOnlyList<MatchEventRecord> Events { get; }

        public void Number(int seq)
        {
            if (Seq != 0)
                throw new InvalidOperationException($"[MatchMetrics] The batch has its number {Seq} already");

            Seq = seq;
        }

        // The declarations of a dropped batch go with the next one: its chunks still name their series by the slots.
        public void TakeSeries(MatchMetricsBatchRecord dropped)
        {
            _series.InsertRange(0, dropped._series);
        }
    }
}
