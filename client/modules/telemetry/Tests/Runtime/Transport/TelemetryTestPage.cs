using Internal;

namespace Global.Telemetry.Tests.Transport
{
    public class TelemetryTestPage : ITelemetryPage
    {
        private readonly EventSource _becameHidden = new EventSource();
        private readonly EventSource<TelemetryPageEvent> _lifecycle = new EventSource<TelemetryPageEvent>();
        private readonly EventSource<TelemetryCanvasSize> _resized = new EventSource<TelemetryCanvasSize>();

        public bool IsHidden { get; private set; }
        public IEventSource BecameHidden => _becameHidden;
        public IEventSource<TelemetryPageEvent> Lifecycle => _lifecycle;
        public IEventSource<TelemetryCanvasSize> Resized => _resized;

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
