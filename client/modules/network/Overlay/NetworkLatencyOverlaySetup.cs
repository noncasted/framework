using System;

namespace Internal
{
    // Creates the NetworkLatencyOverlay of a session for the time its monitor measures (INetworkLatencyMonitor.IsMeasuring):
    // none before the backend knows the local user, destroyed with the end of the measuring.
    public class NetworkLatencyOverlaySetup : IScopeSetup
    {
        public NetworkLatencyOverlaySetup(
            INetworkLatencyMonitor monitor,
            NetworkOverlayOptions options,
            IScreenCanvasCamera screenCanvas)
        {
            if (options.Font == null)
                throw new InvalidOperationException("[Network] NetworkOverlayOptions has no font: set it in the options asset");

            _monitor = monitor;
            _options = options;
            _screenCanvas = screenCanvas;
        }

        private readonly INetworkLatencyMonitor _monitor;
        private readonly NetworkOverlayOptions _options;
        private readonly IScreenCanvasCamera _screenCanvas;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _monitor.IsMeasuring.ViewTrue(lifetime, measuring =>
                NetworkLatencyOverlay.Create(measuring, _monitor, _options.Font, _screenCanvas));
        }
    }
}
