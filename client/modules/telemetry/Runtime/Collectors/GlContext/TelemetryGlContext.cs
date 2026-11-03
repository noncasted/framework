#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using Internal;

namespace Global.Telemetry
{
    // The context of the canvas of Unity (TelemetryGlContext.jslib). Listens from the build of the scope, a session or not. The event
    // runs on the main thread, between the frames.
    public class TelemetryGlContext : ITelemetryGlContext
    {
        public TelemetryGlContext()
        {
            _current = this;
            TelemetryGlContextListen(OnChanged);
        }

        // The callback must be static for AOT. One page, and the latest scope is the one that listens.
        private static TelemetryGlContext _current;

        private readonly EventSource _changed = new();

        public bool IsLost => TelemetryGlContextIsLost();
        public IEventSource Changed => _changed;

        public int TakeLost()
        {
            return TelemetryGlContextTakeLost();
        }

        public int TakeRestored()
        {
            return TelemetryGlContextTakeRestored();
        }

        [MonoPInvokeCallback(typeof(Action))]
        private static void OnChanged()
        {
            _current._changed.Invoke();
        }

        [DllImport("__Internal")]
        private static extern void TelemetryGlContextListen(Action onChanged);

        [DllImport("__Internal")]
        private static extern bool TelemetryGlContextIsLost();

        [DllImport("__Internal")]
        private static extern int TelemetryGlContextTakeLost();

        [DllImport("__Internal")]
        private static extern int TelemetryGlContextTakeRestored();
    }
}
#endif
