using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // IWebSocket over one data channel of a WebRtcPeerLink to another player: the same reliable ordered stream of
    // frames as the socket to the server, so nothing above the socket (reader, writer, commands, session) knows the
    // difference. The two sides negotiate through IWebRtcSignaling; Connect ends when the channel is open, and fails
    // (the caller falls back to the server) when the negotiation does not open it within the timeout or the connection
    // fails.
    public class WebRtcPeerSocket : IWebSocket
    {
        public WebRtcPeerSocket(
            WebRtcPeerRole role,
            IWebRtcSignaling signaling,
            WebRtcPeerOptions options,
            IReadOnlyLifetime lifetime)
        {
            if (options.Channels.Count != 1)
                throw new ArgumentException(
                    $"[Network] [WebRtc] A socket carries one channel, the options declare {options.Channels.Count}", nameof(options));

            _link = new WebRtcPeerLink(role, signaling, options, lifetime);
            _channel = _link.Channels[0];
        }

        private readonly WebRtcPeerLink _link;
        private readonly IWebRtcLinkChannel _channel;

        public IViewableDelegate<byte[]> Received => _channel.Received;
        public IViewableDelegate<SocketCloseInfo> Closed => _link.Closed;

        public UniTask Connect()
        {
            return _link.Connect();
        }

        public UniTask Send(ArraySegment<byte> bytes)
        {
            _channel.Send(bytes);
            return UniTask.CompletedTask;
        }

        public void Drop(string reason)
        {
            _link.Drop(reason);
        }
    }
}
