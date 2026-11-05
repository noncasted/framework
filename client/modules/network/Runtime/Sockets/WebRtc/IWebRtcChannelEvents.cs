namespace Internal
{
    // The events of a connection with several channels (IWebRtcChannels), from any thread.
    public interface IWebRtcChannelEvents
    {
        void OnLocalDescription(string type, string sdp);
        void OnLocalCandidate(string candidate, string mid);

        // Every declared channel is open. Once.
        void OnOpen();

        // The channel is the index in WebRtcPeerOptions.Channels.
        void OnMessage(int channel, byte[] bytes);

        // A channel or the connection is lost (closed by the peer, ICE failed or disconnected, an error). Once.
        void OnLost(string reason);
    }
}
