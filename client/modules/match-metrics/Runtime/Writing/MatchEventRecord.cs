using System.Collections.Generic;

namespace Global.MatchMetrics
{
    // Time: seconds of the clock of the run; AtMs: the real time (IMatchMetricsTicks) of the event.
    public class MatchEventRecord
    {
        public MatchEventRecord(string name, float time, double atMs, IReadOnlyList<MatchEventField> fields)
        {
            Name = name;
            Time = time;
            AtMs = atMs;
            Fields = fields;
        }

        public string Name { get; }
        public float Time { get; }
        public double AtMs { get; }
        public IReadOnlyList<MatchEventField> Fields { get; }
    }
}
