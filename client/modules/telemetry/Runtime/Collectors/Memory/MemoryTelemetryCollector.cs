using Internal;

namespace Global.Telemetry
{
    // A size of zero means the counter does not work here: no metric rather than a false zero. The collections are the
    // growth since the last sample of the session, where zero is a real value.
    public class MemoryTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public MemoryTelemetryCollector(ITelemetryMemoryReadings readings)
        {
            _readings = readings;
            _collections = readings.Collections;
        }

        private readonly ITelemetryMemoryReadings _readings;
        private int _collections;

        public void Start(IReadOnlyLifetime running)
        {
            _collections = _readings.Collections;
        }

        public void Collect(ITelemetrySample sample)
        {
            GaugeSize(sample, "memory.allocated", _readings.Allocated);
            GaugeSize(sample, "memory.reserved", _readings.Reserved);
            GaugeSize(sample, "memory.mono.used", _readings.MonoUsed);
            GaugeSize(sample, "memory.mono.heap", _readings.MonoHeap);
            GaugeSize(sample, "memory.textures", _readings.Textures);

            if (_readings.TextureCount > 0)
                sample.Gauge("memory.texture_count", _readings.TextureCount, TelemetryUnits.Count);

            var collections = _readings.Collections;
            sample.Gauge("gc.collections", collections - _collections, TelemetryUnits.Count);
            _collections = collections;
        }

        private static void GaugeSize(ITelemetrySample sample, string name, long bytes)
        {
            if (bytes > 0)
                sample.Gauge(name, bytes, TelemetryUnits.Bytes);
        }
    }
}
