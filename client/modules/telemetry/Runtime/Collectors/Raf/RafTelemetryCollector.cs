using Internal;

namespace Global.Telemetry
{
    // web.raf.dt.avg and web.raf.dt.max: the frames the browser gives the page. Long ones here with a short frame.cpu.main: the
    // browser or the GPU holds the frames; short ones here with a long frame.dt: Unity does not keep up.
    public class RafTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public RafTelemetryCollector(ITelemetryRaf raf)
        {
            _raf = raf;
        }

        private readonly ITelemetryRaf _raf;

        public void Start(IReadOnlyLifetime running)
        {
            _raf.Start();
            running.Listen(_raf.Stop);
        }

        public void Collect(ITelemetrySample sample)
        {
            var max = _raf.TakeMaxMs();
            var average = _raf.TakeAverageMs();

            if (average < 0d)
                return;

            sample.Gauge("web.raf.dt.avg", average, TelemetryUnits.Milliseconds);
            sample.Gauge("web.raf.dt.max", max, TelemetryUnits.Milliseconds);
        }
    }
}
