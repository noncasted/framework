using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Lobbies
{
    // Network service "Lobby" of a lobby session: its properties belong to the session, not to a player.
    // The key and the property ids are shared with the backend LobbyService (SharedLobbyService).
    // - Lobby (id 1): the room code and the index of the leader. Written only by the backend.
    // - StartState (id 2): written by the client of the leader - "Start" sets IsStarting, "Cancel" clears it. The
    //   backend clears it when the leader changes while it is set.
    // - Countdown (id 3): the countdown the backend runs while StartState is set, 5..0, one step a second on the
    //   clock of the server. Written only by the backend: every client shows the number it has got.
    // - Game (id 4): the match session the backend creates at zero, with a place for every user of the lobby.
    //   Written only by the backend.
    // - Visibility (id 5): "CLOSED / OPEN", written by the client of the leader. A new lobby is closed.
    // A game with properties of its own (SharedLobbyService.GamePropertyFirst and on) derives the service and adds
    // them in CollectProperties.
    public class LobbyNetworkService : NetworkService, INetworkSessionSetupCompleted, IScopeSetup
    {
        public LobbyNetworkService(
            INetworkObjectsCollection objects,
            INetworkConnection connection,
            INetworkSessionCallbacks sessionCallbacks)
        {
            _objects = objects;
            _connection = connection;
            _sessionCallbacks = sessionCallbacks;
        }

        private readonly INetworkObjectsCollection _objects;
        private readonly INetworkConnection _connection;
        private readonly INetworkSessionCallbacks _sessionCallbacks;

        public NetworkProperty<LobbyState> Lobby { get; } = new(SharedLobbyService.LobbyPropertyId);
        public NetworkProperty<LobbyStartState> StartState { get; } = new(SharedLobbyService.StartPropertyId);
        public NetworkProperty<LobbyCountdownState> Countdown { get; } = new(SharedLobbyService.CountdownPropertyId);
        public NetworkProperty<LobbyGameState> Game { get; } = new(SharedLobbyService.GamePropertyId);
        public NetworkProperty<LobbyVisibilityState> Visibility { get; } = new(SharedLobbyService.VisibilityPropertyId);

        // The service registers itself: the scope is set up before the session starts.
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _sessionCallbacks.Register(this);
        }

        // Fetches the service from the session: the current values, updates come after that.
        public UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime)
        {
            var properties = new Dictionary<int, INetworkProperty>
            {
                { SharedLobbyService.LobbyPropertyId, Lobby },
                { SharedLobbyService.StartPropertyId, StartState },
                { SharedLobbyService.CountdownPropertyId, Countdown },
                { SharedLobbyService.GamePropertyId, Game },
                { SharedLobbyService.VisibilityPropertyId, Visibility }
            };

            CollectProperties(properties);

            var resolver = new NetworkServiceExtensions.NetworkServiceResolver<LobbyNetworkService>(
                _objects,
                _connection,
                properties,
                this,
                SharedLobbyService.Key);

            return resolver.OnSessionSetupCompleted(lifetime);
        }

        // The properties of the game, ids from SharedLobbyService.GamePropertyFirst.
        protected virtual void CollectProperties(Dictionary<int, INetworkProperty> properties)
        {
        }
    }
}
