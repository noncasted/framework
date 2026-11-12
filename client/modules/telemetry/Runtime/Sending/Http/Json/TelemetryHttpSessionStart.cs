using System;

namespace Global.Telemetry
{
    // POST <base>/start. Guids as text. hasPrevious false: no previous session (JsonUtility writes no null object).
    [Serializable]
    public class TelemetryHttpSessionStart
    {
        public string linkId;
        public TelemetryHttpDevice device;
        public string[] tags;
        public string startupId;
        public bool hasPrevious;
        public TelemetryHttpPreviousSession previous;
    }
}
