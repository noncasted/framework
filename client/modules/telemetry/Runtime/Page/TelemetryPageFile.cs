using System;

namespace Global.Telemetry
{
    // A PerformanceResourceTiming entry. name: the last segment of the path, without the host and the query (cut on the page). The
    // sizes are 0 for a file of another origin without Timing-Allow-Origin.
    [Serializable]
    public class TelemetryPageFile
    {
        public string name;
        public double start;
        public double end;
        public double transfer;
        public double encoded;
        public double decoded;
    }
}
