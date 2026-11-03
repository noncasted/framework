using Internal;

namespace Global.Telemetry.Tests.Transport
{
    public class TelemetryTestPage : ITelemetryPage
    {
        private readonly EventSource _becameHidden = new EventSource();

        public bool IsHidden { get; private set; }
        public IEventSource BecameHidden => _becameHidden;

        public void Hide()
        {
            IsHidden = true;
            _becameHidden.Invoke();
        }

        public void Show()
        {
            IsHidden = false;
        }
    }
}
