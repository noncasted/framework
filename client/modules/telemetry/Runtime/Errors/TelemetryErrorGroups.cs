using System.Collections.Generic;

namespace Global.Telemetry
{
    // Scrubbed errors by their key, up to the records with a counter. Main thread only.
    public class TelemetryErrorGroups
    {
        public TelemetryErrorGroups(ITelemetryErrors errors, ITelemetryClock clock)
        {
            _errors = errors;
            _clock = clock;
        }

        private readonly ITelemetryErrors _errors;
        private readonly ITelemetryClock _clock;
        private readonly List<TelemetryErrorGroup> _groups = new(TelemetryErrorLimits.MaxGroups);
        private int _records;

        public bool IsSpent => _records >= TelemetryErrorLimits.MaxRecordsPerSession;

        public void Count(TelemetryErrorLevel level, string message, string stack, int repeat)
        {
            var group = Find(level, message, stack);

            if (group == null)
            {
                if (_groups.Count == TelemetryErrorLimits.MaxGroups)
                    return;

                group = new TelemetryErrorGroup(level, message, stack, _clock.ElapsedMilliseconds);
                _groups.Add(group);
            }

            group.Count(repeat);
        }

        public void FlushDue()
        {
            var now = _clock.ElapsedMilliseconds;
            var left = TelemetryErrorLimits.MaxRecordsPerWindow;

            for (var i = 0; i < _groups.Count && left > 0 && IsSpent == false; i++)
            {
                if (_groups[i].IsDue(now) == false)
                    continue;

                Record(_groups[i], now);
                left--;
            }
        }

        // The session is over: there is no next window.
        public void FlushAll()
        {
            var now = _clock.ElapsedMilliseconds;

            for (var i = 0; i < _groups.Count && IsSpent == false; i++)
            {
                if (_groups[i].Pending > 0)
                    Record(_groups[i], now);
            }
        }

        public void Clear()
        {
            _groups.Clear();
        }

        // A new session: nothing of the previous one, its limit fresh.
        public void Reset()
        {
            _groups.Clear();
            _records = 0;
        }

        private TelemetryErrorGroup Find(TelemetryErrorLevel level, string message, string stack)
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].IsSame(level, message, stack))
                    return _groups[i];
            }

            return null;
        }

        private void Record(TelemetryErrorGroup group, long now)
        {
            _records++;
            _errors.Add(group.Level, group.Message, group.Stack, group.TakePending(now));
        }
    }
}
