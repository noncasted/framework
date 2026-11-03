using Internal;

namespace Global.Telemetry
{
    // Outside the web there is no tab to hide.
    public class NoTelemetryPage : ITelemetryPage
    {
        private readonly EventSource _becameHidden = new();
        private readonly EventSource<TelemetryPageEvent> _lifecycle = new();

        public bool IsHidden => false;
        public IEventSource BecameHidden => _becameHidden;
        public IEventSource<TelemetryPageEvent> Lifecycle => _lifecycle;
    }
}
