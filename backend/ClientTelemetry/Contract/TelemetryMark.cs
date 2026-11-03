namespace Game.ClientTelemetry;

[GenerateSerializer]
public sealed record TelemetryMark(long Time, string Name);
