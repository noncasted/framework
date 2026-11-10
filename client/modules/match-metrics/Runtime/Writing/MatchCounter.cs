namespace Global.MatchMetrics
{
    public class MatchCounter : IMatchCounter
    {
        public MatchCounter(MatchMetricsStream stream, int slot)
        {
            _stream = stream;
            _slot = slot;
        }

        private readonly MatchMetricsStream _stream;
        private readonly int _slot;

        public void Add(float value)
        {
            _stream.Add(_slot, value);
        }
    }
}
