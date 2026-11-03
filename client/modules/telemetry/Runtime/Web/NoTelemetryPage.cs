using Internal;

namespace Global.Telemetry
{
    // Outside the web there is no tab to hide.
    public class NoTelemetryPage : ITelemetryPage
    {
        private readonly EventSource _becameHidden = new();

        public bool IsHidden => false;
        public IEventSource BecameHidden => _becameHidden;
    }
}
