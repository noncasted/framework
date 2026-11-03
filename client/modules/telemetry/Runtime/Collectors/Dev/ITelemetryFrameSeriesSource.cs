using Internal;

namespace Global.Telemetry
{
    // The recorders of the profiler for a development build. An opened one lives on the lifetime.
    public interface ITelemetryFrameSeriesSource
    {
        // False: there is no such counter in this build, which is not an error.
        bool TryOpenCounter(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryFrameSeries series);

        // A marker by its name only, in nanoseconds per frame; one that does not exist yet starts with its first sample.
        ITelemetryFrameSeries OpenMarker(IReadOnlyLifetime lifetime, string name);
    }
}
