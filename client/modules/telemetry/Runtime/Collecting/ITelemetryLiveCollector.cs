using Internal;

namespace Global.Telemetry
{
    // A collector with something to start, registered under this interface too: a subscription to the frames, an open
    // counter. Started for every session, `running` ends with it and when the server turns the telemetry off, so nothing
    // goes on per frame outside a session; what is left of the previous session is dropped here. Not called when the
    // telemetry is off in this run. Here a collector also decides what is available: a metric it cannot get just never
    // reaches ITelemetrySample, so its name is never registered.
    public interface ITelemetryLiveCollector
    {
        void Start(IReadOnlyLifetime running);
    }
}
