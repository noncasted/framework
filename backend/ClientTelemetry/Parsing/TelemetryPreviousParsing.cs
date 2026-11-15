namespace Game.ClientTelemetry;

// The previous session after TelemetryBatchParser. Rejected: the parts of it that were dropped.
public sealed record TelemetryPreviousParsing(TelemetryPreviousSession? Session, int Rejected);
