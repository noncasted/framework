namespace Game.MatchMetrics;

// A batch that keeps every rule of the contract: its series are new slots of the stream, its chunks name known slots.
public sealed record ParsedMatchBatch(int Seq, IReadOnlyList<ParsedMatchSeries> Series, IReadOnlyList<ParsedMatchChunk> Chunks, IReadOnlyList<ParsedMatchEvent> Events);
