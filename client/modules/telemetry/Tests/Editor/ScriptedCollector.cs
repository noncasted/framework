using System;

namespace Global.Telemetry.Tests
{
    public class ScriptedCollector : ITelemetryCollector
    {
        private Action<ITelemetrySample> _collect = _ => { };

        public int Calls { get; private set; }

        public void Script(Action<ITelemetrySample> collect)
        {
            _collect = collect;
        }

        public void Collect(ITelemetrySample sample)
        {
            Calls++;
            _collect(sample);
        }
    }
}
