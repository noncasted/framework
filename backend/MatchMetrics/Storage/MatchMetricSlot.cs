using Shared;

namespace Game.MatchMetrics;

public readonly record struct MatchMetricSlot(int SeriesId, SharedMatchMetrics.MatchSeriesKind Kind);
