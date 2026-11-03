using System;

namespace Global.Telemetry.Tests
{
    public class ThrowingCollector : ITelemetryCollector
    {
        public void Collect(ITelemetrySample sample)
        {
            throw new InvalidOperationException("collector");
        }
    }
}
