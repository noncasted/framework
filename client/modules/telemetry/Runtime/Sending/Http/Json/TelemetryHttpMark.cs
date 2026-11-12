using System;

namespace Global.Telemetry
{
    // Empty detail: none. hasDuration false: the mark closes nothing (JsonUtility writes no null number).
    [Serializable]
    public class TelemetryHttpMark
    {
        public long time;
        public string name;
        public string detail;
        public bool hasDuration;
        public long durationMs;
    }
}
