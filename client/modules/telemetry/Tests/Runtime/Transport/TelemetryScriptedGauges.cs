using System;

namespace Global.Telemetry.Tests.Transport
{
    public class TelemetryScriptedGauges : ITelemetryCollector
    {
        private Action<ITelemetrySample> _collect = sample => sample.Gauge("default.value", 1d);

        public void Script(Action<ITelemetrySample> collect)
        {
            _collect = collect;
        }

        public void Collect(ITelemetrySample sample)
        {
            _collect(sample);
        }
    }
}
