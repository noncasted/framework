#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // The local storage of the page (TelemetryTrace.jslib): it outlives a killed WebContent process and a reload, and is written at once.
    // PlayerPrefs of WebGL go to IndexedDB later, too late for a page that dies.
    public class TelemetryWebTraceStore : ITelemetryTraceStore
    {
        public string Read()
        {
            return TelemetryTraceRead();
        }

        public void Write(string text)
        {
            TelemetryTraceWrite(text);
        }

        public void Clear()
        {
            TelemetryTraceClear();
        }

        [DllImport("__Internal")]
        private static extern string TelemetryTraceRead();

        [DllImport("__Internal")]
        private static extern void TelemetryTraceWrite(string text);

        [DllImport("__Internal")]
        private static extern void TelemetryTraceClear();
    }
}
#endif
