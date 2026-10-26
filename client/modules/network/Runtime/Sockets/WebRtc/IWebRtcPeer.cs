using System;
using System.Collections.Generic;

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

    public interface IWebRtcPeerEvents
    {
        void OnLocalDescription(string type, string sdp);
        void OnLocalCandidate(string candidate, string mid);
        void OnOpen();
        void OnMessage(byte[] bytes);

        // The channel or the connection is lost (closed by the peer, ICE failed or disconnected, an error). Once.
        void OnLost(string reason);
    }

    public readonly struct WebRtcPeerOptions
    {
        public WebRtcPeerOptions(IReadOnlyList<string> iceServers, TimeSpan connectTimeout)
        {
            IceServers = iceServers;
            ConnectTimeout = connectTimeout;
        }

        // "stun:host:port" urls. Empty: host candidates only (a local network, a test inside one process).
        public readonly IReadOnlyList<string> IceServers;

        // An attempt that does not open the channel in this time has failed (the caller falls back to the server).
        public readonly TimeSpan ConnectTimeout;
    }

    public static class WebRtcPeers
    {
        public static IWebRtcPeer Create(WebRtcPeerOptions options, IWebRtcPeerEvents events)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new JsWebRtcPeer(options, events);
#else
            return new NativeWebRtcPeer(options, events);
#endif
        }
    }
}
