namespace Game.ClientTelemetry;

[GenerateSerializer]
public sealed record TelemetryStoredMark(long Seq, long Time, string Name, DateTime ReceivedAt);
