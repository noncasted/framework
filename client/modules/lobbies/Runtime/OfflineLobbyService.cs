using System;
using Cysharp.Threading.Tasks;
using Game.Session;
using Internal;
using Shared;
using UnityEngine;

namespace Global.Lobbies
{
    // The match sessions of the offline local game server: created by the lobby of the offline mode when its countdown
    // reaches zero. Implemented by the game (its offline backend).
    public interface IOfflineLobbyMatches
    {
        OfflineLobbyMatch CreateMatch();
    }

    public readonly struct OfflineLobbyMatch
    {
        public OfflineLobbyMatch(string serverUrl, Guid sessionId)
        {
            ServerUrl = serverUrl;
            SessionId = sessionId;
        }

        public readonly string ServerUrl;
        public readonly Guid SessionId;
    }

    // Network service "Lobby" of an offline lobby: the backend Game.Lobbies.LobbyService for one user (see its
    // comments for the contract). The leader is the only user; the start counts down and creates a match on the
    // local game server. Nothing is listed: nobody else can see the lobby, nobody joins a running match late (no lobby
    // of the late players offline: LobbyState.IsRunning is never set).
    public class OfflineLobbyService : Service
    {
        public OfflineLobbyService(string code, IOfflineLobbyMatches matches, ISessionOwner owner) : base(SharedLobbyService.Key)
        {
            _matches = matches;
            _owner = owner;
            _lobby = new ValueProperty<LobbyState>(SharedLobbyService.LobbyPropertyId, new LobbyState { Code = code });

            // The start flag and the visibility - the client of the leader, the rest - only the service.
            _lobby.RestrictWriter(_ => false);
            _start.RestrictWriter(user => user.Index == _ownerIndex);
            _countdown.RestrictWriter(_ => false);
            _game.RestrictWriter(_ => false);
            _visibility.RestrictWriter(user => user.Index == _ownerIndex);

            BindProperty(_lobby);
            BindProperty(_start);
            BindProperty(_countdown);
            BindProperty(_game);
            BindProperty(_visibility);
        }

        private static readonly TimeSpan CountdownStep = TimeSpan.FromSeconds(1);

        private readonly IOfflineLobbyMatches _matches;
        private readonly ISessionOwner _owner;
        private readonly ValueProperty<LobbyState> _lobby;
        private readonly ValueProperty<LobbyStartState> _start = new(SharedLobbyService.StartPropertyId);
        private readonly ValueProperty<LobbyCountdownState> _countdown = new(SharedLobbyService.CountdownPropertyId);
        private readonly ValueProperty<LobbyGameState> _game = new(SharedLobbyService.GamePropertyId);
        private readonly ValueProperty<LobbyVisibilityState> _visibility = new(SharedLobbyService.VisibilityPropertyId);

        private int _ownerIndex;
        private ILifetime _countdownLifetime;

        private bool IsGameStarted => _game.Value.SessionId != Guid.Empty;

        protected override void OnStarted(IReadOnlyLifetime lifetime)
        {
            Debug.Log($"[Lobbies] [Offline] Lobby code {_lobby.Value.Code}");

            _start.Written.Advise(lifetime, OnStartWritten);
            _owner.Owner.View(lifetime, OnOwnerChanged);
        }

        // The only user of the session, from its join.
        private void OnOwnerChanged(IUser owner)
        {
            if (owner == null)
                return;

            _ownerIndex = owner.Index;
            _lobby.Update(state => state.OwnerIndex = owner.Index);
        }

        private void OnStartWritten(LobbyStartState state)
        {
            if (IsGameStarted == true)
                return;

            if (state.IsStarting == _countdown.Value.IsActive)
                return;

            if (state.IsStarting == true)
                StartCountdown();
            else
                StopCountdown();
        }

        private void StartCountdown()
        {
            var countdownLifetime = Lifetime.Child();
            _countdownLifetime = countdownLifetime;

            _countdown.Set(new LobbyCountdownState
            {
                IsActive = true,
                SecondsLeft = LobbyCountdownState.Seconds
            });

            Countdown(countdownLifetime).Forget();
        }

        private void StopCountdown()
        {
            if (_countdown.Value.IsActive == false)
                return;

            _countdownLifetime.Terminate();
            _countdownLifetime = null;
            _countdown.Set(new LobbyCountdownState());
        }

        private async UniTaskVoid Countdown(IReadOnlyLifetime countdownLifetime)
        {
            while (true)
            {
                var isCanceled = await UniTask
                                       .Delay(CountdownStep, DelayType.Realtime, cancellationToken: countdownLifetime.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                var secondsLeft = _countdown.Value.SecondsLeft - 1;

                _countdown.Set(new LobbyCountdownState
                {
                    IsActive = true,
                    SecondsLeft = secondsLeft
                });

                if (secondsLeft > 0)
                    continue;

                StartGame();
                return;
            }
        }

        private void StartGame()
        {
            _countdownLifetime.Terminate();
            _countdownLifetime = null;

            var match = _matches.CreateMatch();

            Debug.Log($"[Lobbies] [Offline] Lobby {_lobby.Value.Code} starts the match {match.SessionId}");

            _game.Set(new LobbyGameState
            {
                ServerUrl = match.ServerUrl,
                SessionId = match.SessionId,
                PlayersCount = 1
            });
        }
    }
}
