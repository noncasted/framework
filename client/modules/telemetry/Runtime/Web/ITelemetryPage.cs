using Internal;

namespace Global.Telemetry
{
    // The browser page around the build. Events only: nothing here is polled per frame.
    public interface ITelemetryPage
    {
        bool IsHidden { get; }

        IEventSource BecameHidden { get; }
    }
}
