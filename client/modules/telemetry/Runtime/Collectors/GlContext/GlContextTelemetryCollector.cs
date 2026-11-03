using Internal;

namespace Global.Telemetry
{
    // Whether the screen of a client that keeps running is alive: a lost context draws nothing and reports no error by itself.
    // Unity stops its player loop while the context is lost, sending included: what is reported here goes out after a restore, if any.
    // Registered after TelemetryErrorPump: it opens the queue of the errors for the session, and Start here may already add one.
    public class GlContextTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public GlContextTelemetryCollector(
            ITelemetryGlContext context,
            TelemetryBuffer buffer,
            ITelemetryClock clock,
            TelemetryErrorQueue errors)
        {
            _context = context;
            _buffer = buffer;
            _clock = clock;
            _errors = errors;
        }

        private const string LostMetric = "web.gl.context_lost";
        private const string LostMark = "webgl.context_lost";
        private const string RestoredMark = "webgl.context_restored";
        private const string LostError = "WebGL context lost";

        private readonly ITelemetryGlContext _context;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryErrorQueue _errors;

        public void Start(IReadOnlyLifetime running)
        {
            Report();
            _context.Changed.Advise(running, Report);
        }

        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge(LostMetric, _context.IsLost ? 1d : 0d);
        }

        // Inside a session a take holds one event, so the order is kept; what waited for this session comes once, whatever its number.
        private void Report()
        {
            if (_context.TakeLost() > 0)
            {
                _buffer.AddMark(_clock.ElapsedMilliseconds, LostMark);
                _errors.Add(TelemetryErrorLevel.Error, LostError, string.Empty);
            }

            if (_context.TakeRestored() > 0)
                _buffer.AddMark(_clock.ElapsedMilliseconds, RestoredMark);
        }
    }
}
