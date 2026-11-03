#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    public class TelemetryWebMemory : ITelemetryWebMemory
    {
        public double Heap => TelemetryWebHeapBytes();
        public double JsUsed => TelemetryWebJsHeapUsed();
        public double JsTotal => TelemetryWebJsHeapTotal();
        public double JsLimit => TelemetryWebJsHeapLimit();

        [DllImport("__Internal")]
        private static extern double TelemetryWebHeapBytes();

        [DllImport("__Internal")]
        private static extern double TelemetryWebJsHeapUsed();

        [DllImport("__Internal")]
        private static extern double TelemetryWebJsHeapTotal();

        [DllImport("__Internal")]
        private static extern double TelemetryWebJsHeapLimit();
    }
}
#endif
