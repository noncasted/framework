namespace Global.Telemetry
{
    // A monotonic clock in fractions of a millisecond, for the time inside a frame. A browser rounds it (5 µs to 1 ms): Stopwatch of a
    // WebGL build steps by performance.now, 0.1 ms in Chromium on a page without cross-origin isolation. A phase is then a mean of
    // rounded values: right over a sample, not for one frame below the step.
    public interface ITelemetryTicks
    {
        double NowMs { get; }
    }
}
