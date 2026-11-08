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
        // opens the channel inside the call, and the signal goes to it. The offer is held until a channel to the user
        // opens: a client that starts listening after its entry (a late player) still answers it.
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
        private readonly Dictionary<int, List<WebRtcSignal>> _held = new();
        private readonly ViewableDelegate<int> _incoming = new();

        public IViewableDelegate<int> Incoming => _incoming;

        public IWebRtcSignaling Open(IReadOnlyLifetime lifetime, int remoteIndex)
        {
            if (_channels.ContainsKey(remoteIndex) == true)
                throw new InvalidOperationException($"[Network] [Peers] A channel to {remoteIndex} is already open");

            _held.Remove(remoteIndex, out var held);
            var channel = new Channel(_connection, remoteIndex, held);
            _channels.Add(remoteIndex, channel);
            lifetime.Listen(() => _channels.Remove(remoteIndex));

            return channel;
        }

        public void OnSignal(SharedSessionPeer.Signal context)
        {
            var signal = ToSignal(context);

            if (_channels.TryGetValue(context.SenderIndex, out var channel) == true)
            {
                channel.Receive(signal);
                return;
            }

            var isHeld = Hold(context.SenderIndex, signal);
            _incoming.Invoke(context.SenderIndex);

            // A held signal went to the channel opened inside the call together with the rest of its negotiation.
            if (isHeld == true)
                return;

            // Nobody wanted the connection: the attempt of the peer times out and it plays through the server.
            if (_channels.TryGetValue(context.SenderIndex, out channel) == false)
                return;

            channel.Receive(signal);
        }

        // An offer starts a new negotiation of the user and replaces the held one; the signals after it join it. Others
        // (an answer, a candidate of a negotiation that was not held) are not held: they belong to a channel that is gone.
        private bool Hold(int senderIndex, WebRtcSignal signal)
        {
            if (signal.Kind == WebRtcSignalKind.Description && signal.Type == "offer")
            {
                _held[senderIndex] = new List<WebRtcSignal> { signal };
                return true;
            }

            if (_held.TryGetValue(senderIndex, out var held) == false)
                return false;

            held.Add(signal);
            return true;
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

        // The held signals of the user go to the first subscriber: the link subscribes after the channel is open.
        private class Channel : IWebRtcSignaling, IViewableDelegate<WebRtcSignal>
        {
            public Channel(INetworkConnection connection, int remoteIndex, List<WebRtcSignal> held)
            {
                _connection = connection;
                _remoteIndex = remoteIndex;
                _held = held;
            }

            private readonly INetworkConnection _connection;
            private readonly int _remoteIndex;
            private readonly ViewableDelegate<WebRtcSignal> _received = new();

            private List<WebRtcSignal> _held;

            public IViewableDelegate<WebRtcSignal> Received => this;

            public void Advise(IReadOnlyLifetime lifetime, Action<WebRtcSignal> handler)
            {
                _received.Advise(lifetime, handler);

                if (_held == null)
                    return;

                var held = _held;
                _held = null;

                foreach (var signal in held)
                    _received.Invoke(signal);
            }

            public void Dispose()
            {
                _received.Dispose();
            }

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
