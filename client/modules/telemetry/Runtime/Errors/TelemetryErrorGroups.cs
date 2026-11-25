using System.Collections.Generic;

namespace Global.Telemetry
{
    // Scrubbed errors by their key, up to the records with a counter. Warnings have their own smaller limits and go after the
    // errors in a window. Main thread only.
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
        private int _warningGroups;
        private int _warningRecords;

        // The errors decide: spent warnings only stop the warnings.
        public bool IsSpent => _records >= TelemetryErrorLimits.MaxRecordsPerSession;

        public void Count(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            var group = Find(level, category, message, stack);

            if (group == null)
            {
                if (IsFull(level))
                    return;

                group = new TelemetryErrorGroup(level, category, message, stack, _clock.ElapsedMilliseconds);
                _groups.Add(group);

                if (level == TelemetryErrorLevel.Warning)
                    _warningGroups++;
            }

            group.Count(repeat);
        }

        public void FlushDue()
        {
            var now = _clock.ElapsedMilliseconds;
            var left = TelemetryErrorLimits.MaxRecordsPerWindow;

            left = FlushDue(now, left, isWarning: false);
            FlushDue(now, left, isWarning: true);
        }

        // The session is over: there is no next window.
        public void FlushAll()
        {
            var now = _clock.ElapsedMilliseconds;

            for (var i = 0; i < _groups.Count && IsSpent == false; i++)
            {
                if (_groups[i].Pending > 0 && CanRecord(_groups[i]))
                    Record(_groups[i], now);
            }
        }

        public void Clear()
        {
            _groups.Clear();
            _warningGroups = 0;
        }

        // A new session: nothing of the previous one, its limit fresh.
        public void Reset()
        {
            Clear();
            _records = 0;
            _warningRecords = 0;
        }

        private int FlushDue(long now, int left, bool isWarning)
        {
            for (var i = 0; i < _groups.Count && left > 0 && IsSpent == false; i++)
            {
                var group = _groups[i];

                if ((group.Level == TelemetryErrorLevel.Warning) != isWarning || group.IsDue(now) == false || CanRecord(group) == false)
                    continue;

                Record(group, now);
                left--;
            }

            return left;
        }

        private bool IsFull(TelemetryErrorLevel level)
        {
            if (level == TelemetryErrorLevel.Warning)
                return _warningGroups == TelemetryErrorLimits.MaxWarningGroups;

            return _groups.Count - _warningGroups == TelemetryErrorLimits.MaxGroups;
        }

        private bool CanRecord(TelemetryErrorGroup group)
        {
            return group.Level != TelemetryErrorLevel.Warning || _warningRecords < TelemetryErrorLimits.MaxWarningRecordsPerSession;
        }

        private TelemetryErrorGroup Find(TelemetryErrorLevel level, string category, string message, string stack)
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].IsSame(level, category, message, stack))
                    return _groups[i];
            }

            return null;
        }

        private void Record(TelemetryErrorGroup group, long now)
        {
            if (group.Level == TelemetryErrorLevel.Warning)
                _warningRecords++;
            else
                _records++;

            _errors.Add(group.Level, group.Category, group.Message, group.Stack, group.TakePending(now));
        }
    }
}
