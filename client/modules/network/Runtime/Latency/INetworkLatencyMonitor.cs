using Shared;

namespace Internal
{
    public interface INetworkLatencyMonitor
    {
        // True from the first round of probes (the backend knows the local user) to the end of the session: what a view
        // of the report (NetworkLatencyOverlay of Internal.Network.Overlay) lives on.
        IViewableProperty<bool> IsMeasuring { get; }

        // Text of the overlay, rebuilt twice a second.
        string Report { get; }

        // The last round trip to the backend, 0 before the first pong.
        float PingMs { get; }

        void OnPong(int id);
        void OnPeerEcho(SharedSessionLatency.PeerEcho echo);
    }
}
