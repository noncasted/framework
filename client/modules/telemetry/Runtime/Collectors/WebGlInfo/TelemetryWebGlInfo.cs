#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryWebGlInfo.jslib.
    public class TelemetryWebGlInfo : ITelemetryWebGlInfo
    {
        public int Version => TelemetryWebGlRead(0);
        public int MaxTextureSize => TelemetryWebGlRead(1);
        public int Antialias => TelemetryWebGlRead(2);
        public int PreserveDrawingBuffer => TelemetryWebGlRead(3);
        public int PowerPreference => TelemetryWebGlRead(4);
        public int TimerQuery => TelemetryWebGlRead(5);

        [DllImport("__Internal")]
        private static extern int TelemetryWebGlRead(int what);
    }
}
#endif
