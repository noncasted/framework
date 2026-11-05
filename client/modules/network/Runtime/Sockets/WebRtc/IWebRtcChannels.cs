using System;

namespace Internal
{
    // A platform peer connection with several data channels declared before the negotiation
    // (WebRtcPeerOptions.Channels). Raw, as IWebRtcPeer: no framing, no threads policy - the events come through
    // IWebRtcChannelEvents from any thread, WebRtcPeerLink brings them to the main thread. A channel is addressed by
    // its index in the list of WebRtcPeerOptions.Channels.
    public interface IWebRtcChannels : IDisposable
    {
        // The offering side: creates every declared channel before the negotiation starts.
        void CreateChannels();

        void SetRemoteDescription(string type, string sdp);
        void AddRemoteCandidate(string candidate, string mid);

        // The largest message the channel carries (the smaller of both sides), known once every channel is open.
        int MaxMessageSize(int channel);

        // The bytes queued to send on the channel: a large transfer keeps its memory here until it is sent.
        int BufferedAmount(int channel);

        // A channel that is not open fails loudly.
        void Send(int channel, byte[] message);
    }
}
