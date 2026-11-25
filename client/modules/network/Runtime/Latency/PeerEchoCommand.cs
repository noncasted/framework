using Shared;

namespace Internal
{
    public class PeerEchoCommand : OneWayCommand<SharedSessionLatency.PeerEcho>
    {
        public PeerEchoCommand(INetworkLatencyMonitor monitor)
        {
            _monitor = monitor;
        }

        private readonly INetworkLatencyMonitor _monitor;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionLatency.PeerEcho context)
        {
            _monitor.OnPeerEcho(context);
        }
    }
}
