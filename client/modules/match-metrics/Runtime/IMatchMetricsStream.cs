namespace Global.MatchMetrics
{
    // One writer in one match. A series is declared once (the same name and tags give the same handle) and written every
    // frame if needed: the handles do not allocate. Names: [a-z0-9._] up to 64; tags: MatchTags. A wrong name or a series
    // over the limit throws (SharedMatchMetricsLimits). Writing after the end of the stream throws.
    public interface IMatchMetricsStream
    {
        IMatchCounter Counter(string name, MatchTags tags = default);
        IMatchGauge Gauge(string name, MatchTags tags = default);

        // A rare thing at the current time of the clock: a pick, a death, a pause. Up to 8 fields.
        void Event(string name, params MatchEventField[] fields);
    }
}
