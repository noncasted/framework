using System;

namespace Global.Telemetry
{
    // The answer to the start of a session. The id comes from the server and is kept only for this session.
    public readonly struct TelemetrySessionAnswer
    {
        private TelemetrySessionAnswer(TelemetrySendOutcome outcome, Guid sessionId)
        {
            Outcome = outcome;
            SessionId = sessionId;
        }

        public TelemetrySendOutcome Outcome { get; }
        public Guid SessionId { get; }

        public static TelemetrySessionAnswer Accepted(Guid sessionId)
        {
            return new TelemetrySessionAnswer(TelemetrySendOutcome.Accepted, sessionId);
        }

        public static TelemetrySessionAnswer Refused(TelemetrySendOutcome outcome)
        {
            if (outcome == TelemetrySendOutcome.Accepted)
                throw new ArgumentException("[Telemetry] Accepted needs the id of the session", nameof(outcome));

            return new TelemetrySessionAnswer(outcome, Guid.Empty);
        }
    }
}
