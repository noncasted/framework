using System;

namespace Global.Telemetry.Tests
{
    public class TelemetryCall
    {
        public TelemetryCall(TelemetryCallKind kind, Guid sessionId, TelemetrySessionStart start, TelemetryBatch batch, TelemetryStartupTrace startupTrace = null)
        {
            Kind = kind;
            SessionId = sessionId;
            Start = start;
            Batch = batch;
            StartupTrace = startupTrace;
        }

        public TelemetryCallKind Kind { get; }
        public Guid SessionId { get; }
        public TelemetrySessionStart Start { get; }
        public TelemetryBatch Batch { get; }
        public TelemetryStartupTrace StartupTrace { get; }
    }
}
