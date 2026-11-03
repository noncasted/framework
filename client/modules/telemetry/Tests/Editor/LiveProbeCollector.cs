using Internal;

namespace Global.Telemetry.Tests
{
    public class LiveProbeCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public LiveProbeCollector(string name)
        {
            _name = name;
        }

        private readonly string _name;

        public int Starts { get; private set; }
        public int Collects { get; private set; }
        public IReadOnlyLifetime Running { get; private set; }

        public void Start(IReadOnlyLifetime running)
        {
            Starts++;
            Running = running;
        }

        public void Collect(ITelemetrySample sample)
        {
            Collects++;
            sample.Gauge(_name, Collects);
        }
    }
}
