namespace Global.Telemetry
{
    public class WebMemoryTelemetryCollector : ITelemetryCollector
    {
        public WebMemoryTelemetryCollector(ITelemetryWebMemory memory)
        {
            _memory = memory;
        }

        private readonly ITelemetryWebMemory _memory;

        public void Collect(ITelemetrySample sample)
        {
            GaugeSize(sample, "web.wasm.heap", _memory.Heap);
            GaugeSize(sample, "web.js.used", _memory.JsUsed);
            GaugeSize(sample, "web.js.total", _memory.JsTotal);
            GaugeSize(sample, "web.js.limit", _memory.JsLimit);
        }

        private static void GaugeSize(ITelemetrySample sample, string name, double bytes)
        {
            if (bytes >= 0d)
                sample.Gauge(name, bytes, TelemetryUnits.Bytes);
        }
    }
}
