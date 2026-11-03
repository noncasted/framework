using Internal;

namespace Global.Telemetry
{
    // Whether the tab was hidden for at least a part of the time since the last question: the numbers of such a sample
    // say nothing about the performance of the game, and the frames stop altogether in a hidden tab, so the first sample
    // after the return covers the whole time away.
    public class TelemetryVisibility
    {
        public TelemetryVisibility(ITelemetryPage page)
        {
            _page = page;
        }

        private readonly ITelemetryPage _page;
        private bool _wasHidden;

        // A session asks from its own start.
        public void Listen(IReadOnlyLifetime lifetime)
        {
            _wasHidden = false;
            _page.BecameHidden.Advise(lifetime, OnHidden);
        }

        public bool TakeWasHidden()
        {
            var wasHidden = _wasHidden || _page.IsHidden;
            _wasHidden = false;
            return wasHidden;
        }

        private void OnHidden()
        {
            _wasHidden = true;
        }
    }
}
