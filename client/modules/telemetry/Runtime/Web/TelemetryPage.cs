#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using Internal;

namespace Global.Telemetry
{
    // The visibility and the life of the browser page (TelemetryPage.jslib). The events run on the main thread, between the frames.
    public class TelemetryPage : ITelemetryPage
    {
        public TelemetryPage()
        {
            _current = this;
            TelemetryPageListen(OnBecameHidden, OnLifecycle);
        }

        // The callbacks must be static for AOT. One page, and the latest scope is the one that listens.
        private static TelemetryPage _current;

        private readonly EventSource _becameHidden = new();
        private readonly EventSource<TelemetryPageEvent> _lifecycle = new();

        public bool IsHidden => TelemetryPageIsHidden();
        public IEventSource BecameHidden => _becameHidden;
        public IEventSource<TelemetryPageEvent> Lifecycle => _lifecycle;

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnBecameHidden()
        {
            _current._becameHidden.Invoke();
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnLifecycle(int kind)
        {
            _current._lifecycle.Invoke((TelemetryPageEvent)kind);
        }

        [DllImport("__Internal")]
        private static extern void TelemetryPageListen(Action onBecameHidden, Action<int> onLifecycle);

        [DllImport("__Internal")]
        private static extern bool TelemetryPageIsHidden();
    }
}
#endif
