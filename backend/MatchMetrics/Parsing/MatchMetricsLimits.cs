namespace Game.MatchMetrics;

// What a client may send: the names, tags and fields are the limits of the client module (Global.MatchMetrics), the sizes
// are a wide margin over a batch of 30 s with a buffer of ten minutes.
public static class MatchMetricsLimits
{
    public const int MaxNameLength = 64;
    public const int MaxTagLength = 32;
    public const int MaxTags = 4;
    public const int MaxFields = 8;
    public const int MaxSeries = 256;

    public const int MaxChunks = 4096;
    public const int MaxValues = 65536;
    public const int MaxEvents = 1024;

    // A bucket is a second of the clock of the run: a day of a run is far past any match.
    public const int MaxBucket = 86400;

    // A batch older than a day is not a reconnect.
    public const long MaxAgeMs = 86_400_000;
}
