namespace Global.MatchMetrics
{
    // A monotonic real clock in milliseconds.
    public interface IMatchMetricsTicks
    {
        double NowMs { get; }
    }
}
