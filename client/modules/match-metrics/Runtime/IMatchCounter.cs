namespace Global.MatchMetrics
{
    // A series of sums: a bucket holds the sum of what was added in its second.
    public interface IMatchCounter
    {
        void Add(float value);
    }
}
