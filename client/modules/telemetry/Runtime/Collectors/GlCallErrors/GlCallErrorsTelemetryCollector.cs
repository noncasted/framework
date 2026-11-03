using System.Collections.Generic;
using System.Globalization;
using Internal;

namespace Global.Telemetry
{
    // The errors of WebGL with the call that made them. web.gl.emscripten_errors: the errors emscripten recorded itself in its gl*
    // functions (an enum or a value it rejects before the context is asked), which no getError of the context shows. Under the
    // check of every call (a page opened with ?glcheck: slow, a diagnostic mode) web.gl.check_errors counts the failing calls. The
    // first error of each source, code and call of the page is a mark webgl.emscripten.<code> / webgl.check.<code> with the call
    // (and its arguments and frame under the check) and an error with the time of the page. Registered after TelemetryErrorPump.
    public class GlCallErrorsTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public GlCallErrorsTelemetryCollector(ITelemetryGlCallErrors errors, TelemetryBuffer buffer, ITelemetryClock clock, TelemetryErrorQueue queue)
        {
            _errors = errors;
            _buffer = buffer;
            _clock = clock;
            _queue = queue;
        }

        public const int MaxMarks = 16;

        private readonly ITelemetryGlCallErrors _errors;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryErrorQueue _queue;
        private readonly List<TelemetryGlCallError> _details = new();
        private int _marks;

        public void Start(IReadOnlyLifetime running)
        {
            _marks = 0;
        }

        public void Collect(ITelemetrySample sample)
        {
            var counts = _errors.Take(_details);

            sample.Gauge("web.gl.emscripten_errors", counts.Recorded, TelemetryUnits.Count);

            if (counts.IsChecking)
                sample.Gauge("web.gl.check_errors", counts.Checked, TelemetryUnits.Count);

            foreach (var detail in _details)
                Report(detail);

            _details.Clear();
        }

        private void Report(TelemetryGlCallError error)
        {
            var call = error.IsChecked ? $"{error.Call}({error.Arguments})" : error.Call;
            var place = error.IsChecked ? $"frame {error.Frame}, " : string.Empty;
            var pageMs = error.PageMs.ToString("0", CultureInfo.InvariantCulture);

            if (_marks < MaxMarks)
            {
                _marks++;
                var prefix = error.IsChecked ? "webgl.check." : "webgl.emscripten.";
                _buffer.AddMark(_clock.ElapsedMilliseconds, prefix + TelemetryGlErrorCodes.MarkSuffixOf(error.Code), call);
            }

            var source = error.IsChecked ? string.Empty : " (emscripten)";
            var message = $"WebGL error {TelemetryGlErrorCodes.ConstantOf(error.Code)} in {error.Call}{source}";
            _queue.Add(TelemetryErrorLevel.Error, message, $"{call}\n{place}{pageMs} ms since the start of the page");
        }
    }
}
