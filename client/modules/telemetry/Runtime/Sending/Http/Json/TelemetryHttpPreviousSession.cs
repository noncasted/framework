using System;

namespace Global.Telemetry
{
    // The previous session of TelemetryHttpSessionStart: samples by the slots of sampleNames.
    [Serializable]
    public class TelemetryHttpPreviousSession
    {
        public string sessionId;
        public string[] tags;
        public long lastSampleTime;
        public long sinceMs;
        public TelemetryHttpMark[] marks;
        public string[] sampleNames;
        public TelemetryHttpSample[] samples;
    }
}
