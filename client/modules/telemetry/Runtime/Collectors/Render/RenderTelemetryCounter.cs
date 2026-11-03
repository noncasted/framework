namespace Global.Telemetry
{
    // An open recorder of a counter a release build does not feed reads zero for ever: the metric appears with the first
    // value, and after it zero is a value.
    public class RenderTelemetryCounter
    {
        public RenderTelemetryCounter(string metric, ITelemetryStat stat)
        {
            _metric = metric;
            _stat = stat;
        }

        private readonly string _metric;
        private readonly ITelemetryStat _stat;
        private bool _hasWorked;

        public void Collect(ITelemetrySample sample)
        {
            var value = _stat.LastValue;

            if (value > 0)
                _hasWorked = true;

            if (_hasWorked)
                sample.Gauge(_metric, value, TelemetryUnits.Count);
        }
    }
}
