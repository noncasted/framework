namespace Global.Telemetry
{
    // Milliseconds of one frame; zero is no value.
    public readonly struct TelemetryFrameTiming
    {
        public TelemetryFrameTiming(double cpuMs, double cpuMainMs, double gpuMs)
        {
            CpuMs = cpuMs;
            CpuMainMs = cpuMainMs;
            GpuMs = gpuMs;
        }

        public double CpuMs { get; }
        public double CpuMainMs { get; }
        public double GpuMs { get; }
    }
}
