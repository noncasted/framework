namespace Global.MatchMetrics
{
    public class MatchGauge : IMatchGauge
    {
        public MatchGauge(MatchMetricsStream stream, int slot)
        {
            _stream = stream;
            _slot = slot;
        }

        private readonly MatchMetricsStream _stream;
        private readonly int _slot;

        public void Set(float value)
        {
            _stream.Set(_slot, value);
        }
    }
}
