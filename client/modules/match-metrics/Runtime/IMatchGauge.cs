namespace Global.MatchMetrics
{
    // A series of states: a bucket holds the last value set in its second; a bucket with nothing set is unchanged.
    public interface IMatchGauge
    {
        void Set(float value);
    }
}
