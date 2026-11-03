using Internal;

namespace Global.Telemetry
{
    // web.gl.version, web.gl.max_texture_size, web.gl.antialias, web.gl.preserve_buffer, web.gl.power_preference,
    // web.gl.timer_query: what the context of the game is, read at the start of the session and sent with every sample (the
    // device of the contract holds none of it: a field there changes the protocol of every game).
    public class WebGlInfoTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public WebGlInfoTelemetryCollector(ITelemetryWebGlInfo info)
        {
            _info = info;
        }

        private static readonly string[] _names =
        {
            "web.gl.version", "web.gl.max_texture_size", "web.gl.antialias", "web.gl.preserve_buffer", "web.gl.power_preference",
            "web.gl.timer_query"
        };

        private readonly ITelemetryWebGlInfo _info;
        private readonly int[] _values = new int[_names.Length];

        public void Start(IReadOnlyLifetime running)
        {
            _values[0] = _info.Version;
            _values[1] = _info.MaxTextureSize;
            _values[2] = _info.Antialias;
            _values[3] = _info.PreserveDrawingBuffer;
            _values[4] = _info.PowerPreference;
            _values[5] = _info.TimerQuery;
        }

        public void Collect(ITelemetrySample sample)
        {
            for (var i = 0; i < _names.Length; i++)
            {
                if (_values[i] >= 0)
                    sample.Gauge(_names[i], _values[i]);
            }
        }
    }
}
