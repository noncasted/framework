using UnityEngine;

namespace Global.Telemetry
{
    // Outside a browser: the screen of the player, no canvas.
    public class UnityTelemetryRenderSize : ITelemetryRenderSize
    {
        public int Width => Screen.width;
        public int Height => Screen.height;
        public int CanvasWidth => -1;
        public int CanvasHeight => -1;
        public double Dpr => -1d;
    }
}
