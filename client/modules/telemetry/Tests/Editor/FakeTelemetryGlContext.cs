using Internal;

namespace Global.Telemetry.Tests
{
    // The page side of TelemetryGlContext: the counters stay here until taken.
    public class FakeTelemetryGlContext : ITelemetryGlContext
    {
        private readonly EventSource _changed = new EventSource();
        private int _lost;
        private int _restored;

        public bool IsLost { get; private set; }
        public IEventSource Changed => _changed;

        public int TakeLost()
        {
            var lost = _lost;
            _lost = 0;
            return lost;
        }

        public int TakeRestored()
        {
            var restored = _restored;
            _restored = 0;
            return restored;
        }

        public void Lose()
        {
            IsLost = true;
            _lost++;
            _changed.Invoke();
        }

        public void Restore()
        {
            IsLost = false;
            _restored++;
            _changed.Invoke();
        }
    }
}
