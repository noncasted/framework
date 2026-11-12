namespace Game.MatchMetrics;

// Values: NaN only in a gauge (unchanged in its bucket).
public sealed record ParsedMatchChunk(int Slot, int Start, long AgeMs, float[] Values);
