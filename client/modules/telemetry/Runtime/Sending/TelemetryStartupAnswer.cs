using System;

namespace Global.Telemetry
{
    // The answer to a startup trace. The id comes from the server and is good while the connection lives.
    public readonly struct TelemetryStartupAnswer
    {
        public TelemetryStartupAnswer(TelemetrySendOutcome outcome, Guid startupId)
        {
            Outcome = outcome;
            StartupId = startupId;
        }

        public TelemetrySendOutcome Outcome { get; }
        public Guid StartupId { get; }
    }
}
