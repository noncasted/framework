namespace Global.Telemetry.Tests
{
    public class FirstCollector : ITelemetryCollector
    {
        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge("first", 1);
        }
    }
}
