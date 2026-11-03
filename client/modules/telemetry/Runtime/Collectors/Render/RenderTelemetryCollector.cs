using System.Collections.Generic;
using Internal;

namespace Global.Telemetry
{
    // The counters of the last frame before the sample. A counter the platform does not have gets no metric: a release
    // WebGL may have none of them, which is expected and not logged.
    public class RenderTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public RenderTelemetryCollector(ITelemetryStatSource source)
        {
            _source = source;
        }

        private const string _category = "Render";

        private static readonly (string Stat, string Metric)[] _stats =
        {
            ("Draw Calls Count", "render.draw_calls"),
            ("SetPass Calls Count", "render.setpass"),
            ("Triangles Count", "render.triangles"),
            ("Vertices Count", "render.vertices"),
            ("Used Textures Count", "render.textures"),
            ("Render Textures Count", "render.render_textures"),
        };

        private readonly ITelemetryStatSource _source;
        private readonly List<RenderTelemetryCounter> _counters = new();

        public void Start(IReadOnlyLifetime running)
        {
            foreach (var (name, metric) in _stats)
            {
                if (_source.TryOpen(running, _category, name, out var stat))
                    _counters.Add(new RenderTelemetryCounter(metric, stat));
            }

            running.Listen(_counters.Clear);
        }

        public void Collect(ITelemetrySample sample)
        {
            for (var i = 0; i < _counters.Count; i++)
                _counters[i].Collect(sample);
        }
    }
}
