namespace Global.Telemetry
{
    // The size the frame is drawn at. Zero or below is not known.
    public interface ITelemetryRenderSize
    {
        // The pixels of the drawing buffer: what the GPU fills.
        int Width { get; }
        int Height { get; }

        // The size of the canvas on the page in CSS pixels, and the ratio applied between them; not known outside a browser.
        int CanvasWidth { get; }
        int CanvasHeight { get; }
        double Dpr { get; }
    }
}
