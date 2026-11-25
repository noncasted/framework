using System;

namespace Internal
{
    // A platform peer connection with one reliable ordered data channel: the browser RTCPeerConnection (JsWebRtcPeer)
    // or libdatachannel (NativeWebRtcPeer). Raw: no framing, no threads policy - the events come through
    // IWebRtcPeerEvents from any thread, WebRtcPeerSocket brings them to the main thread.
    public interface IWebRtcPeer : IDisposable
    {
        // The largest message the channel carries (the smaller of both sides), known once it is open.
        int MaxMessageSize { get; }

        // The offering side: creates the channel, the negotiation starts with a local offer.
        void CreateChannel(string label);

        void SetRemoteDescription(string type, string sdp);
        void AddRemoteCandidate(string candidate, string mid);

        // A channel that is not open fails loudly.
        void Send(byte[] message);
    }
}
