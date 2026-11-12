using System;

namespace Global.Telemetry
{
    // The answer to /startup.
    [Serializable]
    public class TelemetryHttpStartupResult
    {
        public bool enabled;
        public string startupId;
    }
}
