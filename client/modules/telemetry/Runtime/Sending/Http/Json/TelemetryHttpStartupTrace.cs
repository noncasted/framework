using System;

namespace Global.Telemetry
{
    // POST <base>/startup.
    [Serializable]
    public class TelemetryHttpStartupTrace
    {
        public string linkId;
        public string startupId;
        public TelemetryHttpDevice device;
        public string name;
        public bool final;
        public double durationMs;
        public int frames;
        public TelemetryHttpStartupSpan[] spans;
    }
}
