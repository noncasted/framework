#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using Internal;

namespace Global.Telemetry
{
    // The visibility of the browser page (TelemetryPage.jslib). The event runs on the main thread, between the frames.
    public class TelemetryPage : ITelemetryPage
    {
        public TelemetryPage()
        {
            _current = this;
            TelemetryPageListen(OnBecameHidden);
        }

        // The callback must be static for AOT. One page, and the latest scope is the one that listens.
        private static TelemetryPage _current;

        private readonly EventSource _becameHidden = new();

        public bool IsHidden => TelemetryPageIsHidden();
        public IEventSource BecameHidden => _becameHidden;

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnBecameHidden()
        {
            _current._becameHidden.Invoke();
        }

        [DllImport("__Internal")]
        private static extern void TelemetryPageListen(Action onBecameHidden);

        [DllImport("__Internal")]
        private static extern bool TelemetryPageIsHidden();
    }
}
#endif
