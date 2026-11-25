using System.Collections.Generic;
using Internal;

namespace Global.Telemetry
{
    // The records of the logger at or above the mark level of their category (LogOptions) as marks of the session:
    // log.debug / log.info / log.warning / log.error, the detail "ShortName: template" (no arguments: no personal data). The
    // time is the one of the call; the marks reach the buffer on the main thread with the next sample and at the end of the
    // session. A flood between two samples keeps its first MaxPending marks.
    public class LogMarkTelemetryCollector : ILogSink, ITelemetryCollector, ITelemetryLiveCollector
    {
        public LogMarkTelemetryCollector(TelemetryBuffer buffer, ITelemetryClock clock)
        {
            _buffer = buffer;
            _clock = clock;
        }

        public const int MaxPending = 64;

        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly object _lock = new();
        private readonly List<PendingMark> _pending = new(MaxPending);
        private readonly List<PendingMark> _taken = new(MaxPending);

        public void Start(IReadOnlyLifetime running)
        {
            lock (_lock)
                _pending.Clear();

            Logs.AddSink(running, this);
            running.Listen(Flush);
        }

        public void Write(in LogRecord record)
        {
            if (record.IsMark == false)
                return;

            var mark = new PendingMark(_clock.ElapsedMilliseconds, NameOf(record.Level), DetailOf(record));

            lock (_lock)
            {
                if (_pending.Count < MaxPending)
                    _pending.Add(mark);
            }
        }

        public void Collect(ITelemetrySample sample)
        {
            Flush();
        }

        private void Flush()
        {
            lock (_lock)
            {
                _taken.AddRange(_pending);
                _pending.Clear();
            }

            foreach (var mark in _taken)
                _buffer.AddMark(mark.Time, mark.Name, mark.Detail);

            _taken.Clear();
        }

        private static string NameOf(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "log.debug",
                LogLevel.Info => "log.info",
                LogLevel.Warning => "log.warning",
                _ => "log.error"
            };
        }

        private static string DetailOf(in LogRecord record)
        {
            var category = record.Category;
            var dot = category.LastIndexOf('.');
            var shortName = dot < 0 ? category : category.Substring(dot + 1);

            return shortName + ": " + (record.Template ?? record.Exception.GetType().Name);
        }

        private readonly struct PendingMark
        {
            public PendingMark(long time, string name, string detail)
            {
                Time = time;
                Name = name;
                Detail = detail;
            }

            public long Time { get; }
            public string Name { get; }
            public string Detail { get; }
        }
    }
}
