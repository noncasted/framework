using System;
using System.Collections.Generic;

namespace Internal
{
    // A link of the mesh over the data channels of a direct connection: the channels of WebRtcPeerLink, framed by it.
    // The received frames come through the inbox of the pair, subscribed since the connection was created.
    internal sealed class DirectPeerLink : IPeerLink
    {
        public DirectPeerLink(
            WebRtcPeerLink link,
            PeerLinkInbox inbox,
            int remoteIndex,
            Guid remoteId,
            IReadOnlyLifetime lifetime)
        {
            _channels = link.Channels;
            _inbox = inbox;
            RemoteIndex = remoteIndex;
            RemoteId = remoteId;
            Lifetime = lifetime;
        }

        private readonly IReadOnlyList<IWebRtcLinkChannel> _channels;
        private readonly PeerLinkInbox _inbox;

        public int RemoteIndex { get; }
        public Guid RemoteId { get; }
        public PeerRoute Route => PeerRoute.Direct;
        public IReadOnlyLifetime Lifetime { get; }

        public void Send(int channel, ArraySegment<byte> bytes)
        {
            _channels[Channel(channel)].Send(bytes);
        }

        public IViewableDelegate<byte[]> Received(int channel)
        {
            return _inbox.Received(Channel(channel));
        }

        public int BufferedAmount(int channel)
        {
            return _channels[Channel(channel)].BufferedAmount;
        }

        private int Channel(int channel)
        {
            if (channel < 0 || channel >= _channels.Count)
                throw new ArgumentOutOfRangeException(nameof(channel), channel,
                    $"[Network] [Mesh] The connection to {RemoteIndex} has no channel {channel}");

            return channel;
        }
    }
}
