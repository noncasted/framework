using System;

namespace Global.Telemetry
{
    // JSON has no NaN: a slot without a value is listed in empty (its index in values) and written as 0 in values.
    [Serializable]
    public class TelemetryHttpSample
    {
        public long time;
        public double[] values;
        public int[] empty;
        public bool hidden;
    }
}
