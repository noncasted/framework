using System;
using System.Collections.Generic;
using Shared;

namespace Internal
{
    // The negotiation of the direct connections of this client, over the control connection of a P2P match
    // (SessionControl): one channel per other user (by its index in the server session), the backend relays the
    // signals (SharedSessionPeer.Signal).
    public interface IPeerSignaling
    {
        // A signal of a user without an open channel: the first offer of a peer. A listener that wants the connection
        // opens the channel inside the call, and the signal goes to it.
        IViewableDelegate<int> Incoming { get; }

        IWebRtcSignaling Open(IReadOnlyLifetime lifetime, int remoteIndex);
    }

    public class PeerSignaling : IPeerSignaling
    {
        public PeerSignaling(INetworkConnection connection)
        {
            _connection = connection;
        }

        private readonly INetworkConnection _connection;
        private readonly Dictionary<int, Channel> _channels = new();
        private readonly ViewableDelegate<int> _incoming = new();

        public IViewableDelegate<int> Incoming => _incoming;

        public IWebRtcSignaling Open(IReadOnlyLifetime lifetime, int remoteIndex)
        {
            if (_channels.ContainsKey(remoteIndex) == true)
                throw new InvalidOperationException($"[Network] [Peers] A channel to {remoteIndex} is already open");

            var channel = new Channel(_connection, remoteIndex);
            _channels.Add(remoteIndex, channel);
            lifetime.Listen(() => _channels.Remove(remoteIndex));

            return channel;
        }

        public void OnSignal(SharedSessionPeer.Signal context)
        {
            if (_channels.ContainsKey(context.SenderIndex) == false)
                _incoming.Invoke(context.SenderIndex);

            // Nobody wanted the connection: the attempt of the peer times out and it plays through the server.
            if (_channels.TryGetValue(context.SenderIndex, out var channel) == false)
                return;

            channel.Receive(ToSignal(context));
        }

        private static WebRtcSignal ToSignal(SharedSessionPeer.Signal context)
        {
            return context.Kind switch
            {
                SharedSessionPeer.SignalKind.Description => WebRtcSignal.Description(context.Type, context.Value),
                SharedSessionPeer.SignalKind.Candidate => WebRtcSignal.Candidate(context.Value, context.Mid),
                _ => throw new ArgumentOutOfRangeException(nameof(context), context.Kind, null)
            };
        }

        private class Channel : IWebRtcSignaling
        {
            public Channel(INetworkConnection connection, int remoteIndex)
            {
                _connection = connection;
                _remoteIndex = remoteIndex;
            }

            private readonly INetworkConnection _connection;
            private readonly int _remoteIndex;
            private readonly ViewableDelegate<WebRtcSignal> _received = new();

            public IViewableDelegate<WebRtcSignal> Received => _received;

            public void Send(WebRtcSignal signal)
            {
                _connection.OneWay(new SharedSessionPeer.Signal
                {
                    TargetIndex = _remoteIndex,
                    Kind = signal.Kind == WebRtcSignalKind.Description
                        ? SharedSessionPeer.SignalKind.Description
                        : SharedSessionPeer.SignalKind.Candidate,
                    Type = signal.Type,
                    Value = signal.Value,
                    Mid = signal.Mid
                });
            }

            public void Receive(WebRtcSignal signal)
            {
                _received.Invoke(signal);
            }
        }
    }
}
