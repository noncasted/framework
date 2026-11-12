using System;

namespace Global.Telemetry
{
    // level by name: Error, Exception, Warning.
    [Serializable]
    public class TelemetryHttpError
    {
        public long time;
        public string level;
        public string message;
        public string stack;
        public int repeat;
    }
}
