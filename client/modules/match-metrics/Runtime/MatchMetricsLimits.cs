namespace Global.MatchMetrics
{
    // The limits of a stream, the same the server applies to what it accepts.
    public static class MatchMetricsLimits
    {
        public const int MaxNameLength = 64;
        public const int MaxTagLength = 32;
        public const int MaxTags = 4;
        public const int MaxFields = 8;
        public const int MaxSeries = 256;
    }
}
