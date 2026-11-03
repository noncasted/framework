#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
using Internal;

namespace Global.Telemetry
{
    // The errors only the page sees: window.onerror and unhandledrejection (TelemetryBrowserErrors.jslib). They come on the
    // main thread, between the frames, and take the way of any other error.
    public class TelemetryBrowserErrors : ITelemetryLiveCollector
    {
        public TelemetryBrowserErrors(TelemetryErrorQueue queue)
        {
            _queue = queue;
        }

        // Generic delegates do not cross into the page.
        private delegate void ErrorCallback(string message, string stack);

        // The callback must be static for AOT. One page, and the latest scope is the one that listens.
        private static TelemetryErrorQueue _current;

        private readonly TelemetryErrorQueue _queue;

        public void Start(IReadOnlyLifetime running)
        {
            _current = _queue;
            TelemetryBrowserErrorsListen(OnError);
            running.Listen(TelemetryBrowserErrorsStop);
        }

        [MonoPInvokeCallback(typeof(ErrorCallback))]
        private static void OnError(string message, string stack)
        {
            _current.Add(TelemetryErrorLevel.Exception, message, stack);
        }

        [DllImport("__Internal")]
        private static extern void TelemetryBrowserErrorsListen(ErrorCallback onError);

        [DllImport("__Internal")]
        private static extern void TelemetryBrowserErrorsStop();
    }
}
#endif
