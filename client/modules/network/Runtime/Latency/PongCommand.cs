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
}
