using Internal;

namespace Global.Telemetry
{
    // The warnings and errors of the logger of the framework that the console shows, with their category and their template as
    // the message: the same call site is one error whatever its arguments, and the arguments (the personal data) are not sent.
    // An exception comes with its type and text and its stack. The lines the logger puts into the log of Unity are left out
    // there (TelemetryLogErrors). From any thread: only the queue is touched.
    public class TelemetryLoggerErrors : ILogSink, ITelemetryLiveCollector
    {
        public TelemetryLoggerErrors(TelemetryErrorQueue queue)
        {
            _queue = queue;
        }

        private readonly TelemetryErrorQueue _queue;

        public void Start(IReadOnlyLifetime running)
        {
            Logs.AddSink(running, this);
        }

        public void Write(in LogRecord record)
        {
            if (record.IsShown == false || record.Level < LogLevel.Warning || TelemetrySelfLog.IsInside)
                return;

            var exception = record.Exception;

            if (exception == null)
            {
                _queue.Add(LevelOf(record.Level), record.Category, record.Template, null);
                return;
            }

            var text = exception.GetType().FullName + ": " + exception.Message;
            var message = record.Template == null ? text : record.Template + "\n" + text;

            _queue.Add(TelemetryErrorLevel.Exception, record.Category, message, exception.StackTrace);
        }

        private static TelemetryErrorLevel LevelOf(LogLevel level)
        {
            return level == LogLevel.Warning ? TelemetryErrorLevel.Warning : TelemetryErrorLevel.Error;
        }
    }
}
