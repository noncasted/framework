namespace Global.MatchMetrics
{
    // A stream with its sender, from its opening until the sender has nothing more to do: the sending of what is left
    // outlives the stream.
    public class MatchMetricsEntry
    {
        public MatchMetricsEntry(MatchMetricsStream stream, MatchMetricsSender sender, double openedMs)
        {
            _stream = stream;
            _sender = sender;
            _lastCutMs = openedMs;
        }

        private readonly MatchMetricsStream _stream;
        private readonly MatchMetricsSender _sender;

        private double _lastCutMs;

        public bool IsDone => _stream.IsEnded && _sender.IsIdle;

        public void Tick(double nowMs, double sendIntervalMs)
        {
            if (_stream.IsEnded == false)
            {
                _stream.Sync();

                if (nowMs - _lastCutMs >= sendIntervalMs)
                {
                    _lastCutMs = nowMs;
                    Enqueue(_stream.Cut(false));
                }
            }

            _sender.Pump();
        }

        public void End()
        {
            Enqueue(_stream.Cut(true));
        }

        private void Enqueue(MatchMetricsBatchRecord batch)
        {
            if (batch != null)
                _sender.Enqueue(batch);
        }
    }
}
