using System;

namespace Global.Telemetry
{
    // POST <base>/end.
    [Serializable]
    public class TelemetryHttpSessionEnd
    {
        public string linkId;
        public string sessionId;
    }
}
