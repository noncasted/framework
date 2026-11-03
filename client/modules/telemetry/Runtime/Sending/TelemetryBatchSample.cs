using System.Collections.Generic;

namespace Global.Telemetry
{
    public readonly struct TelemetryBatchSample
    {
        public TelemetryBatchSample(long time, IReadOnlyList<double> values, bool isHidden)
        {
            Time = time;
            Values = values;
            IsHidden = isHidden;
        }

        public long Time { get; }

        // By the slots of the names. NaN: no value in this sample.
        public IReadOnlyList<double> Values { get; }

        // The page was in the background for at least a part of the sample.
        public bool IsHidden { get; }
    }
}
