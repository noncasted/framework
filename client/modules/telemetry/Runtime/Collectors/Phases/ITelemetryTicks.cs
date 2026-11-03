namespace Global.Telemetry
{
    // A monotonic clock in fractions of a millisecond, for the time inside a frame. A browser rounds it (5 µs to 1 ms).
    public interface ITelemetryTicks
    {
        double NowMs { get; }
    }
}
