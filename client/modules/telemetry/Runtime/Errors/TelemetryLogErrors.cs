using Internal;
using UnityEngine;

namespace Global.Telemetry
{
    // Errors of the log of Unity, the unobserved exceptions of UniTask among them:
    // the fork logs them while nobody listens to UniTaskScheduler.UnobservedTaskException, so nobody here does. Warnings go as
    // their own level under their own limits (a font without a glyph, a texture that failed); plain logs are never sent. The lines of the logger of the framework
    // (Logs.IsWriting) came with their category and template through TelemetryLoggerErrors already. Comes
    // from any thread: only the queue is touched here.
    public class TelemetryLogErrors : ITelemetryLiveCollector
    {
        public TelemetryLogErrors(TelemetryErrorQueue queue)
        {
            _queue = queue;
        }

        private readonly TelemetryErrorQueue _queue;

        public void Start(IReadOnlyLifetime running)
        {
            Application.logMessageReceivedThreaded += OnLog;
            running.Listen(() => Application.logMessageReceivedThreaded -= OnLog);
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log || TelemetrySelfLog.IsInside || Logs.IsWriting)
                return;

            _queue.Add(LevelOf(type), message, stack);
        }

        private static TelemetryErrorLevel LevelOf(LogType type)
        {
            return type switch
            {
                LogType.Exception => TelemetryErrorLevel.Exception,
                LogType.Warning => TelemetryErrorLevel.Warning,
                _ => TelemetryErrorLevel.Error
            };
        }
    }
}
