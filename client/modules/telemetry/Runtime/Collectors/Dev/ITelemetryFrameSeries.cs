namespace Global.Telemetry
{
    // The values of a counter or a marker of the profiler, one per frame, since the previous take.
    public interface ITelemetryFrameSeries
    {
        // The number of the frames taken: 0 when none came, then the sum and the maximum say nothing.
        int Take(out double sum, out double max);
    }
}
