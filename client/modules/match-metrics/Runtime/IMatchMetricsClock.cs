namespace Global.MatchMetrics
{
    // The clock of the run, given by the game: the same for every player of a match, it stands while the game is paused.
    // A bucket is one second of it.
    public interface IMatchMetricsClock
    {
        float Seconds { get; }
    }
}
