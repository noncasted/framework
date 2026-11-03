namespace Game.ClientTelemetry;

// ParentIndex: below its own index, -1 for a root.
[GenerateSerializer]
public sealed record TelemetryStartupSpan(int ParentIndex, string Name, double StartMs, double DurationMs, int Frames, bool Unfinished, long AllocBytes);
