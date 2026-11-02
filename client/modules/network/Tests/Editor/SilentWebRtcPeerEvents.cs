namespace Internal.Tests
{
    // The events of a peer no test looks at.
    public class SilentWebRtcPeerEvents : IWebRtcPeerEvents
    {
        public void OnLocalDescription(string type, string sdp)
        {
        }

        public void OnLocalCandidate(string candidate, string mid)
        {
        }

        public void OnOpen()
        {
        }

        public void OnMessage(byte[] bytes)
        {
        }

        public void OnLost(string reason)
        {
        }
    }
}
