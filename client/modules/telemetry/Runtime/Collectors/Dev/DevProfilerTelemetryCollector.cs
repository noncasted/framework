using System.Collections.Generic;
using System.Text;
using Internal;

namespace Global.Telemetry
{
    // A development build only (StandardTelemetryCollectorsExtensions): the counters a release build does not feed and the markers of
    // the profiler the game lists (ClientTelemetryOptions.DevProfilerMarkers), as dev.* per frame of the sample: the average and .max.
    // A development build is slower than a release one: its numbers are compared with each other only.
    public class DevProfilerTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public DevProfilerTelemetryCollector(ITelemetryFrameSeriesSource source, ClientTelemetryOptions options)
        {
            _source = source;
            _options = options;
        }

        public const string MarkerPrefix = "dev.marker.";

        private const double NanosecondsToMs = 1e-6;
        private const string MaxSuffix = ".max";

        private static readonly (string Category, string Stat, string Metric, string Unit)[] _counters =
        {
            ("Memory", "GC Allocated In Frame", "dev.gc.allocated_in_frame", TelemetryUnits.Bytes),
            ("Memory", "GC Allocation In Frame Count", "dev.gc.allocations_in_frame", TelemetryUnits.Count),
            ("Render", "Render Textures Bytes", "dev.render.render_texture_bytes", TelemetryUnits.Bytes),
            ("Render", "Render Textures Changes Count", "dev.render.render_texture_changes", TelemetryUnits.Count),
            ("Render", "Used Buffers Count", "dev.render.buffers", TelemetryUnits.Count),
            ("Render", "Used Buffers Bytes", "dev.render.buffer_bytes", TelemetryUnits.Bytes),
            ("Render", "Vertex Buffer Upload In Frame Bytes", "dev.render.vertex_upload_bytes", TelemetryUnits.Bytes),
            ("Render", "Index Buffer Upload In Frame Bytes", "dev.render.index_upload_bytes", TelemetryUnits.Bytes),
            ("Render", "Shadow Casters Count", "dev.render.shadow_casters", TelemetryUnits.Count),
        };

        private readonly ITelemetryFrameSeriesSource _source;
        private readonly ClientTelemetryOptions _options;
        private readonly List<(ITelemetryFrameSeries Series, string Metric, string MaxMetric, string Unit, double Scale)> _open = new();

        public void Start(IReadOnlyLifetime running)
        {
            foreach (var (category, stat, metric, unit) in _counters)
            {
                if (_source.TryOpenCounter(running, category, stat, out var series))
                    _open.Add((series, metric, metric + MaxSuffix, unit, 1d));
            }

            foreach (var marker in _options.DevProfilerMarkers)
            {
                var metric = MarkerMetric(marker);
                _open.Add((_source.OpenMarker(running, marker), metric, metric + MaxSuffix, TelemetryUnits.Milliseconds, NanosecondsToMs));
            }

            running.Listen(_open.Clear);
        }

        public void Collect(ITelemetrySample sample)
        {
            for (var i = 0; i < _open.Count; i++)
            {
                var (series, metric, maxMetric, unit, scale) = _open[i];
                var frames = series.Take(out var sum, out var max);

                if (frames == 0)
                    continue;

                sample.Gauge(metric, sum / frames * scale, unit);
                sample.Gauge(maxMetric, max * scale, unit);
            }
        }

        // "Shader.CreateGPUProgram" is dev.marker.shader.creategpuprogram: lower case, any other character than [a-z0-9.] an underscore,
        // cut to leave room for the suffix of the maximum.
        public static string MarkerMetric(string marker)
        {
            var name = new StringBuilder(MarkerPrefix);

            foreach (var symbol in marker.ToLowerInvariant())
                name.Append(symbol is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' ? symbol : '_');

            var length = TelemetryLimits.MaxNameLength - MaxSuffix.Length;

            return name.Length > length ? name.ToString(0, length) : name.ToString();
        }
    }
}
