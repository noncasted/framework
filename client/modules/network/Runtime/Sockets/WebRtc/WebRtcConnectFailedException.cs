using System;

namespace Internal
{
    // WebRtcPeerSocket.Connect: the direct connection did not open. An expected outcome: the caller goes through the
    // server instead.
    public class WebRtcConnectFailedException : Exception
    {
        public WebRtcConnectFailedException(string reason) : base($"[Network] [WebRtc] The peer did not connect: {reason}")
        {
        }
    }
}
