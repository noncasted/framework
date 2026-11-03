using System;
using Internal;

namespace Global.Telemetry
{
    // The errors of WebGL in a release build, where nobody else asks: web.gl.errors is the number of the codes taken in the sample,
    // the first ones of a session leave a mark by the code, and every code is an error of the session (the groups count the repeats).
    // OUT_OF_MEMORY is the one a page that loses its textures or its text is expected to show. web.gl.errors_ms: how long the
    // synchronous loop of the take kept the main thread, high when the GPU is behind. Registered after TelemetryErrorPump.
    public class GlErrorsTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public GlErrorsTelemetryCollector(ITelemetryGlErrors errors, TelemetryBuffer buffer, ITelemetryClock clock, TelemetryErrorQueue queue)
        {
            _errors = errors;
            _buffer = buffer;
            _clock = clock;
            _queue = queue;
        }

        public const int MaxCodesPerSample = 8;
        public const int MaxMarks = 16;

        private const string Metric = "web.gl.errors";
        private const string TimeMetric = "web.gl.errors_ms";

        private readonly ITelemetryGlErrors _errors;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryErrorQueue _queue;
        private readonly int[] _codes = new int[MaxCodesPerSample];
        private int _marks;

        public void Start(IReadOnlyLifetime running)
        {
            _marks = 0;
        }

        public void Collect(ITelemetrySample sample)
        {
            var count = _errors.Take(_codes);

            sample.Gauge(Metric, count, TelemetryUnits.Count);
            sample.Gauge(TimeMetric, _errors.LastTakeMs, TelemetryUnits.Milliseconds);

            for (var i = 0; i < count; i++)
                Report(_codes[i]);

            Array.Clear(_codes, 0, count);
        }

        private void Report(int code)
        {
            var (mark, message) = NamesOf(code);

            if (_marks < MaxMarks)
            {
                _marks++;
                _buffer.AddMark(_clock.ElapsedMilliseconds, mark);
            }

            _queue.Add(TelemetryErrorLevel.Error, message, string.Empty);
        }

        private static (string Mark, string Message) NamesOf(int code)
        {
            return code switch
            {
                0x0500 => ("webgl.error.invalid_enum", "WebGL error INVALID_ENUM"),
                0x0501 => ("webgl.error.invalid_value", "WebGL error INVALID_VALUE"),
                0x0502 => ("webgl.error.invalid_operation", "WebGL error INVALID_OPERATION"),
                0x0505 => ("webgl.error.out_of_memory", "WebGL error OUT_OF_MEMORY"),
                0x0506 => ("webgl.error.invalid_framebuffer_operation", "WebGL error INVALID_FRAMEBUFFER_OPERATION"),
                0x9242 => ("webgl.error.context_lost", "WebGL error CONTEXT_LOST_WEBGL"),
                _ => ("webgl.error.other", "WebGL error of another code")
            };
        }
    }
}
