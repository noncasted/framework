using System;

namespace Global.Telemetry
{
    // POST <base>/batch.
    [Serializable]
    public class TelemetryHttpBatch
    {
        public string linkId;
        public string sessionId;
        public TelemetryHttpName[] names;
        public TelemetryHttpSample[] samples;
        public TelemetryHttpError[] errors;
        public TelemetryHttpMark[] marks;
    }
}
