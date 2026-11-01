using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Internal
{
    // The connection of a player of a P2P match to the server session of the match: its control plane. The session of
    // the game lives on the host (its local server); the server session keeps the seats of the players (the
    // reservations, the reconnect window, the listing, the late entry, the rewards), relays the negotiation of the
    // direct connections (Signaling) and the frames of the guests without one (Tunnels), and carries the services the
    // host reports to (the stage of the run). Its own socket, beside the connection of the game session.
    public interface ISessionControl
    {
        // The index and the id of this player in the server session (its seat: the same in the session of the host).
        int LocalIndex { get; }
        Guid LocalId { get; }

        // The other players connected to the server session now, by index (an away seat is not here until it returns).
        IViewableDictionary<int, Guid> Peers { get; }

        IPeerSignaling Signaling { get; }
        IPeerTunnels Tunnels { get; }

        // The connection is lost: the match is lost for this player (a return into it follows).
        IViewableDelegate<SocketCloseInfo> Closed { get; }

        // Enters the server session with the ticket of the meta; completes once the local seat is known.
        UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket);

        // A service of the server session (GetRequest): its object id and the values now; the updates of its
        // properties then go to the handler.
        UniTask<SharedSessionService.GetResponse> GetService(
            IReadOnlyLifetime lifetime,
            string key,
            IReadOnlyList<int> propertyIds,
            Action<SharedSessionObject.PropertyUpdate> onUpdate);

        void WriteProperty(int objectId, int propertyId, byte[] value);

        // The player leaves the match on purpose: the server ends its seat at once (no reconnect window).
        UniTask Leave();
    }

    public class SessionControl : ISessionControl
    {
        public SessionControl(
            PlatformOptions platformOptions,
            IClientOutdatedNotice outdatedNotice,
            ILocalServers localServers,
            IPeerSockets peerSockets,
            NetworkProtocolHash protocolHash)
        {
            var commands = new NetworkCommandsCollection();
            _connection = new NetworkConnection(commands, platformOptions, outdatedNotice, localServers, peerSockets, protocolHash);

            _signaling = new PeerSignaling(_connection);
            _tunnels = new PeerTunnels(_connection);

            commands.Add(new HandlerCommand<SharedSessionPlayer.LocalUpdate>(OnLocalUpdate));
            commands.Add(new HandlerCommand<SharedSessionPlayer.RemoteUpdate>(OnRemoteUpdate));
            commands.Add(new HandlerCommand<SharedSessionPlayer.RemoteAway>(OnRemoteAway));
            commands.Add(new HandlerCommand<SharedSessionPlayer.RemoteDisconnect>(OnRemoteDisconnect));
            commands.Add(new HandlerCommand<SharedSessionPeer.Signal>(_signaling.OnSignal));
            commands.Add(new HandlerCommand<SharedSessionPeer.Tunnel>(_tunnels.OnFrame));
            commands.Add(new HandlerCommand<SharedSessionObject.PropertyUpdate>(OnPropertyUpdate));
            commands.Add(new HandlerCommand<SharedSessionLatency.Pong>(_ => { }));

            // The latency probes of the other players are answered: their overlay measures this leg too.
            commands.Add(new HandlerCommand<SharedSessionLatency.PeerProbe>(probe => _connection.OneWay(new SharedSessionLatency.PeerEcho
            {
                Id = probe.Id,
                SenderIndex = probe.SenderIndex,
                ServerTimestamp = probe.ServerTimestamp
            })));
        }

        // As NetworkSession: the server sends the local seat right after the authorization.
        private static readonly TimeSpan LocalSeatTimeout = TimeSpan.FromSeconds(20);

        private readonly NetworkConnection _connection;
        private readonly PeerSignaling _signaling;
        private readonly PeerTunnels _tunnels;
        private readonly ViewableDictionary<int, Guid> _peers = new();
        private readonly Dictionary<int, Action<SharedSessionObject.PropertyUpdate>> _objects = new();

        private int _localIndex;
        private Guid _localId;

        public int LocalIndex => _localIndex != 0 ? _localIndex : throw new InvalidOperationException("[Network] [Control] No local seat yet");
        public Guid LocalId => _localId;
        public IViewableDictionary<int, Guid> Peers => _peers;
        public IPeerSignaling Signaling => _signaling;
        public IPeerTunnels Tunnels => _tunnels;
        public IViewableDelegate<SocketCloseInfo> Closed => _connection.Closed;

        public async UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
        {
            Debug.Log($"[Network] [Control] Entering the server session at {serverUrl}");

            await new SessionConnection(_connection).Connect(lifetime, serverUrl, ticket);

            await UniTask
                  .WaitUntil(() => _localIndex != 0 || _connection.Writer.IsClosed == true, cancellationToken: lifetime.Token)
                  .Timeout(LocalSeatTimeout, DelayType.Realtime);

            if (_localIndex == 0)
                throw new OperationCanceledException("[Network] [Control] The connection is lost before the local seat");

            Debug.Log($"[Network] [Control] Local seat {_localIndex}");
        }

        public async UniTask<SharedSessionService.GetResponse> GetService(
            IReadOnlyLifetime lifetime,
            string key,
            IReadOnlyList<int> propertyIds,
            Action<SharedSessionObject.PropertyUpdate> onUpdate)
        {
            var response = await _connection.Request<SharedSessionService.GetResponse>(new SharedSessionService.GetRequest
            {
                Key = key,
                PropertiesIds = propertyIds
            });

            _objects.Add(response.Id, onUpdate);
            lifetime.Listen(() => _objects.Remove(response.Id));

            return response;
        }

        public void WriteProperty(int objectId, int propertyId, byte[] value)
        {
            _connection.OneWay(new SharedSessionObject.SetProperty
            {
                ObjectId = objectId,
                PropertyId = propertyId,
                Value = value
            });
        }

        public async UniTask Leave()
        {
            _connection.OneWay(new SharedSessionPlayer.Leave());
            await _connection.ForceSendAll();
        }

        private void OnLocalUpdate(SharedSessionPlayer.LocalUpdate update)
        {
            if (_localIndex != 0)
                throw new InvalidOperationException($"[Network] [Control] A second LocalUpdate ({update.Index})");

            _localIndex = update.Index;
            _localId = update.Id;
        }

        // A known index: the user has returned before the server noticed its loss. Its frames belong to a new connection,
        // the tunnel of the old one is lost.
        private void OnRemoteUpdate(SharedSessionPlayer.RemoteUpdate update)
        {
            _tunnels.OnPeerGone(update.Index);
            _peers[update.Index] = update.Id;
        }

        // The player has lost its connection to the server (its seat waits for its return): no frame of its tunnel comes
        // any more, the tunnel is lost at once instead of by the silence of the session of the game.
        private void OnRemoteAway(SharedSessionPlayer.RemoteAway away)
        {
            _peers.Remove(away.Index);
            _tunnels.OnPeerGone(away.Index);
        }

        // A seat that was away first is already out of the peers.
        private void OnRemoteDisconnect(SharedSessionPlayer.RemoteDisconnect disconnect)
        {
            if (_peers.ContainsKey(disconnect.Index) == true)
                _peers.Remove(disconnect.Index);

            _tunnels.OnPeerGone(disconnect.Index);
        }

        // An update of a service this player has not fetched never comes (the server sends them to the subscribers).
        private void OnPropertyUpdate(SharedSessionObject.PropertyUpdate update)
        {
            if (_objects.TryGetValue(update.ObjectId, out var handler) == false)
                throw new InvalidOperationException($"[Network] [Control] An update of unknown object {update.ObjectId}");

            handler(update);
        }

        private class HandlerCommand<T> : OneWayCommand<T> where T : INetworkContext
        {
            public HandlerCommand(Action<T> handler)
            {
                _handler = handler;
            }

            private readonly Action<T> _handler;

            protected override void Execute(IReadOnlyLifetime lifetime, T context)
            {
                _handler(context);
            }
        }
    }
}
