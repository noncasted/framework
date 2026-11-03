using Internal;

namespace Global.Telemetry
{
    public interface ITelemetryStatSource
    {
        // False: there is no such counter on this platform, which is not an error. An opened one lives on the lifetime.
        bool TryOpen(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryStat stat);
    }
}
