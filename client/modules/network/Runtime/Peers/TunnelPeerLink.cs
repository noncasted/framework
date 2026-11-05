using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // A link of the mesh over the tunnel of the server: the tunnel is one ordered stream of frames, so the channels are
    // multiplexed into it by the first byte of a frame (the index of a channel). The unreliable channel goes reliably
    // here: a tunnel has no other mode.
    internal sealed class TunnelPeerLink : IPeerLink
    {
        // A frame that opens the tunnel on the other side: it belongs to no channel and carries no payload.
        public const byte Hello = 0xFF;

        public TunnelPeerLink(
            IWebSocket tunnel,
            int remoteIndex,
            Guid remoteId,
            IReadOnlyLifetime lifetime,
            IReadOnlyList<WebRtcChannelSpec> channels)
        {
            _tunnel = tunnel;
            RemoteIndex = remoteIndex;
            RemoteId = remoteId;
            Lifetime = lifetime;
            _received = new ViewableDelegate<byte[]>[channels.Count];

            for (var i = 0; i < _received.Length; i++)
                _received[i] = new ViewableDelegate<byte[]>();

            tunnel.Received.Advise(lifetime, OnFrame);
            lifetime.Listen(Dispose);
        }

        private readonly IWebSocket _tunnel;
        private readonly ViewableDelegate<byte[]>[] _received;

        public int RemoteIndex { get; }
        public Guid RemoteId { get; }
        public PeerRoute Route => PeerRoute.Tunnel;
        public IReadOnlyLifetime Lifetime { get; }

        public void Send(int channel, ArraySegment<byte> bytes)
        {
            var frame = new byte[bytes.Count + 1];
            frame[0] = (byte)Channel(channel);

            if (bytes.Count > 0)
                Buffer.BlockCopy(bytes.Array, bytes.Offset, frame, 1, bytes.Count);

            _tunnel.Send(frame).NoAwait();
        }

        public IViewableDelegate<byte[]> Received(int channel)
        {
            return _received[Channel(channel)];
        }

        public int BufferedAmount(int channel)
        {
            Channel(channel);
            return 0;
        }

        private void OnFrame(byte[] frame)
        {
            if (frame.Length == 0)
                throw new InvalidOperationException($"[Network] [Mesh] An empty frame of the tunnel to {RemoteIndex}");

            if (frame[0] == Hello)
                return;

            if (frame[0] >= _received.Length)
                throw new InvalidOperationException($"[Network] [Mesh] The tunnel to {RemoteIndex} has no channel {frame[0]}");

            var payload = new byte[frame.Length - 1];
            Buffer.BlockCopy(frame, 1, payload, 0, payload.Length);
            _received[frame[0]].Invoke(payload);
        }

        private int Channel(int channel)
        {
            if (channel < 0 || channel >= _received.Length)
                throw new ArgumentOutOfRangeException(nameof(channel), channel,
                    $"[Network] [Mesh] The tunnel to {RemoteIndex} has no channel {channel}");

            return channel;
        }

        private void Dispose()
        {
            foreach (var received in _received)
                received.Dispose();
        }
    }
}
