using Shared;

namespace Internal
{
    public class PongCommand : OneWayCommand<SharedSessionLatency.Pong>
    {
        public PongCommand(INetworkLatencyMonitor monitor)
        {
            _monitor = monitor;
        }

        private readonly INetworkLatencyMonitor _monitor;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionLatency.Pong context)
        {
            _monitor.OnPong(context.Id);
        }
    }

    // A probe of another client: answered at once, the echo goes back through the backend.
    public class PeerProbeCommand : OneWayCommand<SharedSessionLatency.PeerProbe>
    {
        public PeerProbeCommand(INetworkConnection connection, INetworkLatencyMonitor monitor)
        {
            _connection = connection;
            _monitor = monitor;
        }

        private readonly INetworkConnection _connection;
        private readonly INetworkLatencyMonitor _monitor;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionLatency.PeerProbe context)
        {
            var echo = EchoOf(context);
            echo.ResponderPingMs = _monitor.PingMs;
            _connection.OneWay(echo);
        }

        // The answer to a probe of another player, also from a client without a latency monitor (a peer host).
        public static SharedSessionLatency.PeerEcho EchoOf(SharedSessionLatency.PeerProbe probe)
        {
            return new SharedSessionLatency.PeerEcho
            {
                Id = probe.Id,
                SenderIndex = probe.SenderIndex,
                ServerTimestamp = probe.ServerTimestamp
            };
        }
    }

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
