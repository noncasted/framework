using System;

namespace Global.Telemetry
{
    // The answer to /start.
    [Serializable]
    public class TelemetryHttpSessionStarted
    {
        public bool enabled;
        public string sessionId;
    }
}
