namespace Global.Telemetry
{
    // The canvas of the game in CSS pixels and its drawing buffer in pixels: the ratio is the scale the browser draws the buffer at.
    public readonly struct TelemetryCanvasSize
    {
        public TelemetryCanvasSize(int cssWidth, int cssHeight, int bufferWidth, int bufferHeight)
        {
            CssWidth = cssWidth;
            CssHeight = cssHeight;
            BufferWidth = bufferWidth;
            BufferHeight = bufferHeight;
        }

        public int CssWidth { get; }
        public int CssHeight { get; }
        public int BufferWidth { get; }
        public int BufferHeight { get; }

        // The detail of a mark: sizes only, nothing of the user.
        public override string ToString()
        {
            return $"css {CssWidth}x{CssHeight} buffer {BufferWidth}x{BufferHeight}";
        }
    }
}
