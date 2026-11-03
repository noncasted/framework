namespace Global.Telemetry
{
    public readonly struct FrameTimeStats
    {
        public FrameTimeStats(int frames, double averageMs, double p50Ms, double p95Ms, double maxMs, double fps, int over33, int over50)
        {
            Frames = frames;
            AverageMs = averageMs;
            P50Ms = p50Ms;
            P95Ms = p95Ms;
            MaxMs = maxMs;
            Fps = fps;
            Over33 = over33;
            Over50 = over50;
        }

        public int Frames { get; }
        public double AverageMs { get; }
        public double P50Ms { get; }
        public double P95Ms { get; }
        public double MaxMs { get; }
        public double Fps { get; }
        public int Over33 { get; }
        public int Over50 { get; }
    }
}
