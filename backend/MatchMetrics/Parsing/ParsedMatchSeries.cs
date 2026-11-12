using Shared;

namespace Game.MatchMetrics;

public sealed record ParsedMatchSeries(int Slot, MatchSeriesKey Key, SharedMatchMetrics.MatchSeriesKind Kind);
