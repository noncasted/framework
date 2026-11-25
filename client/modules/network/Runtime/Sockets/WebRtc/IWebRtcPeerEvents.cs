namespace Internal
{
    public interface IWebRtcPeerEvents
    {
        void OnLocalDescription(string type, string sdp);
        void OnLocalCandidate(string candidate, string mid);
        void OnOpen();
        void OnMessage(byte[] bytes);

        // The channel or the connection is lost (closed by the peer, ICE failed or disconnected, an error). Once.
        void OnLost(string reason);
    }
}
