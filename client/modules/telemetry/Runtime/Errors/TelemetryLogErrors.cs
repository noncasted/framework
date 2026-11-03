using Internal;
using UnityEngine;

namespace Global.Telemetry
{
    // Errors of the log of Unity, ReactiveExceptions (ReactiveLogging) and the unobserved exceptions of UniTask among them:
    // the fork logs them while nobody listens to UniTaskScheduler.UnobservedTaskException, so nobody here does. Plain logs
    // and warnings are never sent. Comes from any thread: only the queue is touched here.
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
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            if (TelemetrySelfLog.IsInside)
                return;

            _queue.Add(type == LogType.Exception ? TelemetryErrorLevel.Exception : TelemetryErrorLevel.Error, message, stack);
        }
    }
}
