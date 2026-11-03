namespace Global.Telemetry
{
    // render.width, render.height, render.pixels: the real resolution of the frame; render.canvas_width, render.canvas_height
    // and render.dpr on a page. The GPU cost of a fill grows with render.pixels.
    public class RenderSizeTelemetryCollector : ITelemetryCollector
    {
        public RenderSizeTelemetryCollector(ITelemetryRenderSize size)
        {
            _size = size;
        }

        private readonly ITelemetryRenderSize _size;

        public void Collect(ITelemetrySample sample)
        {
            var width = _size.Width;
            var height = _size.Height;

            if (width > 0 && height > 0)
            {
                sample.Gauge("render.width", width, TelemetryUnits.Pixels);
                sample.Gauge("render.height", height, TelemetryUnits.Pixels);
                sample.Gauge("render.pixels", (double)width * height, TelemetryUnits.Pixels);
            }

            var canvasWidth = _size.CanvasWidth;
            var canvasHeight = _size.CanvasHeight;

            if (canvasWidth > 0 && canvasHeight > 0)
            {
                sample.Gauge("render.canvas_width", canvasWidth, TelemetryUnits.Pixels);
                sample.Gauge("render.canvas_height", canvasHeight, TelemetryUnits.Pixels);
            }

            var dpr = _size.Dpr;

            if (dpr > 0d)
                sample.Gauge("render.dpr", dpr);
        }
    }
}
