using System.Collections.Generic;

namespace Global.Telemetry.Tests
{
    public class RecordingSample : ITelemetrySample
    {
        public Dictionary<string, double> Values { get; } = new Dictionary<string, double>();
        public Dictionary<string, string> Units { get; } = new Dictionary<string, string>();

        public void Gauge(string name, double value, string unit = null)
        {
            Values[name] = value;
            Units[name] = unit;
        }

        public void Clear()
        {
            Values.Clear();
            Units.Clear();
        }
    }
}
