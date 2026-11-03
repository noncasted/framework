using System;
using Internal;

namespace Global.Telemetry
{
    // The work of the GPU process the game asks for, per sample: compilations and links of the shaders (a shader without a warm-up is
    // compiled when it is first drawn, inside a frame) and the uploads of buffers and textures (a mesh rebuilt every frame shows
    // as buffer bytes, a resized one as bufferData calls, each a new store). A single call over ClientTelemetryOptions.SlowShaderMs
    // is a mark with its duration and the kind of the call; the first ones of a session only. The page counts from its start, so the
    // first sample of the first session holds the compilations of the loading of Unity, and a later one what came after the previous
    // session.
    public class GlCallsTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public GlCallsTelemetryCollector(ITelemetryGlCalls calls, TelemetryBuffer buffer, ITelemetryClock clock)
        {
            _calls = calls;
            _buffer = buffer;
            _clock = clock;
        }

        public const int MaxMarks = 16;

        private const string SlowMark = "webgl.shader.slow";

        private readonly ITelemetryGlCalls _calls;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly double[] _values = new double[TelemetryGlCallsLayout.Length];
        private int _marks;

        public void Start(IReadOnlyLifetime running)
        {
            _marks = 0;
        }

        public void Collect(ITelemetrySample sample)
        {
            var slow = _calls.Take(_values);

            sample.Gauge("web.gl.shader_compiles", _values[TelemetryGlCallsLayout.Compiles], TelemetryUnits.Count);
            sample.Gauge("web.gl.shader_compile_ms", _values[TelemetryGlCallsLayout.CompileMs], TelemetryUnits.Milliseconds);
            sample.Gauge("web.gl.program_links", _values[TelemetryGlCallsLayout.Links], TelemetryUnits.Count);
            sample.Gauge("web.gl.program_link_ms", _values[TelemetryGlCallsLayout.LinkMs], TelemetryUnits.Milliseconds);
            sample.Gauge("web.gl.buffer_bytes", _values[TelemetryGlCallsLayout.BufferBytes], TelemetryUnits.Bytes);
            sample.Gauge("web.gl.buffer_data_calls", _values[TelemetryGlCallsLayout.BufferDataCalls], TelemetryUnits.Count);
            sample.Gauge("web.gl.texture_bytes", _values[TelemetryGlCallsLayout.TextureBytes], TelemetryUnits.Bytes);
            sample.Gauge("web.gl.texture_uploads", _values[TelemetryGlCallsLayout.TextureUploads], TelemetryUnits.Count);

            var time = _clock.ElapsedMilliseconds;

            for (var i = 0; i < slow && _marks < MaxMarks; i++, _marks++)
            {
                var kind = (int)_values[TelemetryGlCallsLayout.FirstSlow + i * 2];
                var durationMs = (long)Math.Round(_values[TelemetryGlCallsLayout.FirstSlow + i * 2 + 1]);

                _buffer.AddMark(time, SlowMark, DetailOf(kind), durationMs);
            }
        }

        private static string DetailOf(int kind)
        {
            return kind switch
            {
                TelemetryGlCallsLayout.SlowCompile => "compile",
                TelemetryGlCallsLayout.SlowCompileStatus => "compile status",
                TelemetryGlCallsLayout.SlowLink => "link",
                TelemetryGlCallsLayout.SlowLinkStatus => "link status",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "[Telemetry] Unknown kind of a slow call of WebGL")
            };
        }
    }
}
