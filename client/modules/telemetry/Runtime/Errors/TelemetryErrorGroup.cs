using System;

namespace Global.Telemetry
{
    // One error of the session: its texts, the repeats not yet sent and when they may go.
    public class TelemetryErrorGroup
    {
        public TelemetryErrorGroup(TelemetryErrorLevel level, string message, string stack, long now)
        {
            Level = level;
            Message = message;
            Stack = stack;
            _window = TelemetryErrorLimits.FirstWindowMs;
            _nextRecord = now + _window;
        }

        private long _window;
        private long _nextRecord;

        public TelemetryErrorLevel Level { get; }
        public string Message { get; }
        public string Stack { get; }
        public int Pending { get; private set; }

        public bool IsDue(long now)
        {
            return Pending > 0 && now >= _nextRecord;
        }

        public bool IsSame(TelemetryErrorLevel level, string message, string stack)
        {
            return Level == level
                   && Stack == stack
                   && string.CompareOrdinal(Message, 0, message, 0, TelemetryErrorLimits.KeyMessageLength) == 0;
        }

        public void Count(int repeat)
        {
            Pending += repeat;
        }

        public int TakePending(long now)
        {
            var taken = Pending;

            Pending = 0;
            _window = Math.Min(_window * 2, TelemetryErrorLimits.MaxWindowMs);
            _nextRecord = now + _window;

            return taken;
        }
    }
}
