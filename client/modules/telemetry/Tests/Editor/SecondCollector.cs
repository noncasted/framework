namespace Global.Telemetry.Tests
{
    public class SecondCollector : ITelemetryCollector
    {
        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge("second", 2);
        }
    }
}
