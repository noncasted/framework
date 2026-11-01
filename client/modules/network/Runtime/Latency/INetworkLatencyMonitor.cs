using Shared;

namespace Internal
{
    public interface INetworkLatencyMonitor
    {
        // Text of the overlay, rebuilt twice a second.
        string Report { get; }

        // The last round trip to the backend, 0 before the first pong.
        float PingMs { get; }

        void OnPong(int id);
        void OnPeerEcho(SharedSessionLatency.PeerEcho echo);
    }
}
