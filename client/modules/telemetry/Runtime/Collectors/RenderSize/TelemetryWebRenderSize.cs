#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryRenderSize.jslib: the drawing buffer of the context of Unity, not the size of the canvas the page sees.
    public class TelemetryWebRenderSize : ITelemetryRenderSize
    {
        public int Width => TelemetryRenderBufferWidth();
        public int Height => TelemetryRenderBufferHeight();
        public int CanvasWidth => TelemetryRenderCanvasWidth();
        public int CanvasHeight => TelemetryRenderCanvasHeight();
        public double Dpr => TelemetryRenderDpr();

        [DllImport("__Internal")]
        private static extern int TelemetryRenderBufferWidth();

        [DllImport("__Internal")]
        private static extern int TelemetryRenderBufferHeight();

        [DllImport("__Internal")]
        private static extern int TelemetryRenderCanvasWidth();

        [DllImport("__Internal")]
        private static extern int TelemetryRenderCanvasHeight();

        [DllImport("__Internal")]
        private static extern double TelemetryRenderDpr();
    }
}
#endif
