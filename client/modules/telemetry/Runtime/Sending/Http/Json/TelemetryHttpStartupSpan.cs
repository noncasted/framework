using System;

namespace Global.Telemetry
{
    // Empty file: not a file of the page.
    [Serializable]
    public class TelemetryHttpStartupSpan
    {
        public int parentIndex;
        public string name;
        public double startMs;
        public double durationMs;
        public int frames;
        public bool unfinished;
        public long allocBytes;
        public string file;
        public long transferBytes;
        public bool cached;
    }
}
