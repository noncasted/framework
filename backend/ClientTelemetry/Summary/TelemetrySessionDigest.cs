namespace Game.ClientTelemetry;

// The compact result of a closed session, kept in its row with the metadata and searched through the collection. The dropped
// counters: what the limits of the memory of the session took out, so the summary does not cover it.
[GenerateSerializer]
public sealed record TelemetrySessionDigest(
    long DurationMs,
    int ErrorRows,
    long ErrorCount,
    int Marks,
    long DroppedSamples,
    long DroppedErrors,
    long DroppedMarks,
    long RejectedRequests,
    TelemetrySessionSummary Summary,
    TelemetrySessionErrorGroups ErrorGroups);
