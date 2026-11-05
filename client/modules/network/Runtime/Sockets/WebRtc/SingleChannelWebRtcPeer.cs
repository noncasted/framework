using System;

namespace Internal
{
    // IWebRtcChannels over a platform peer with one channel: the browser peer (JsWebRtcPeer) carries one, and a
    // WebRtcPeerLink of one channel (the socket of the star of a P2P match) uses it there as it uses the native peer on
    // the desktop. The messages of the channel go with its index 0.
    public class SingleChannelWebRtcPeer : IWebRtcChannels, IWebRtcPeerEvents
    {
        public SingleChannelWebRtcPeer(WebRtcPeerOptions options, IWebRtcChannelEvents events)
        {
            if (options.Channels.Count != 1)
                throw new ArgumentException(
                    $"[Network] [WebRtc] The platform peer carries one channel, the options declare {options.Channels.Count}", nameof(options));

            _options = options;
            _events = events;
            _peer = WebRtcPeers.Create(options, this);
        }

        private readonly WebRtcPeerOptions _options;
        private readonly IWebRtcChannelEvents _events;
        private readonly IWebRtcPeer _peer;

        public void CreateChannels()
        {
            _peer.CreateChannel(_options.Channels[0].Label);
        }

        public void SetRemoteDescription(string type, string sdp)
        {
            _peer.SetRemoteDescription(type, sdp);
        }

        public void AddRemoteCandidate(string candidate, string mid)
        {
            _peer.AddRemoteCandidate(candidate, mid);
        }

        public int MaxMessageSize(int channel)
        {
            CheckChannel(channel);
            return _peer.MaxMessageSize;
        }

        public int BufferedAmount(int channel)
        {
            // The browser peer has no rtcGetBufferedAmount (JsWebRtcPeer.jslib): the mesh of the constructor, the only
            // user, runs on the desktop.
            throw new NotSupportedException("[Network] [WebRtc] The platform peer does not report the buffered amount");
        }

        public void Send(int channel, byte[] message)
        {
            CheckChannel(channel);
            _peer.Send(message);
        }

        public void Dispose()
        {
            _peer.Dispose();
        }

        void IWebRtcPeerEvents.OnLocalDescription(string type, string sdp)
        {
            _events.OnLocalDescription(type, sdp);
        }

        void IWebRtcPeerEvents.OnLocalCandidate(string candidate, string mid)
        {
            _events.OnLocalCandidate(candidate, mid);
        }

        void IWebRtcPeerEvents.OnOpen()
        {
            _events.OnOpen();
        }

        void IWebRtcPeerEvents.OnMessage(byte[] bytes)
        {
            _events.OnMessage(0, bytes);
        }

        void IWebRtcPeerEvents.OnLost(string reason)
        {
            _events.OnLost(reason);
        }

        private static void CheckChannel(int channel)
        {
            if (channel != 0)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "[Network] [WebRtc] The peer carries one channel");
        }
    }
}
