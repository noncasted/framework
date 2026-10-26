using Cluster.Lobbies;
using Common.Extensions;
using Game.Session;
using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// Network service "Lobby" of a lobby session (the key and the property ids are shared with the client: SharedLobbyService).
// - Lobby (LobbyState, id 1): written only here - the code of the room and the index of the lobby leader
//   (the session owner; when the leader leaves, the next user by index).
// - Start (LobbyStartState, id 2): written by the client of the leader - "Start" / "Cancel". Cleared here
//   only when the leader changes while it is set: its writer is gone.
// - Countdown (LobbyCountdownState, id 3): written only here - the countdown that runs while Start is set,
//   one step a second on the clock of the server: every client shows the number it gets.
// - Game (LobbyGameState, id 4): written only here, once, when the countdown reaches zero - the match
//   session with a place reserved for every user of the lobby. From that moment the lobby is sealed:
//   nobody can enter it by the code (the code passes to the match and answers "the game has already
//   started" until the match is closed), the start flag is not read any more.
// - Visibility (LobbyVisibilityState, id 5): written by the client of the leader - "CLOSED / OPEN". A new lobby
//   is closed. Stays when the leader changes.
// The lobby reports itself to the list of open lobbies (ILobbyListings) until it starts its match.
// The lobby of the late players of a running match (LobbySessionInfo.IsRunning, LobbyState.IsRunning) is the same
// service without a leader start: nothing is written by clients, nothing is reported (the match reports itself). When
// the late players get a place in the match is up to the game: a derived service (OnLatePlayersStarted, Admit,
// ClearGame, MarkRunOver); the game adds its own properties from SharedLobbyService.GamePropertyFirst (BindProperty
// in its constructor).
public class LobbyService : Service
{
    public LobbyService(
        ISessionOwner owner,
        ISessionUsers users,
        ISessionAdmission admission,
        ILobbyMatches matches,
        ILobbyListings listings,
        IExecutionQueue executionQueue,
        TimeProvider time,
        LobbySessionInfo info,
        ILogger<LobbyService> logger) : base(SharedLobbyService.Key)
    {
        // The lobby of the late players of a match found by the search: no lobby, no code.
        var isCodeless = info.IsRunning == true && info.LobbySessionId == Guid.Empty;

        if (isCodeless == true ? info.Code.Length != 0 : LobbyCode.IsValid(info.Code) == false)
            throw new InvalidOperationException($"Lobby session is created with an invalid code '{info.Code}'");

        _owner = owner;
        _users = users;
        _admission = admission;
        _matches = matches;
        _listings = listings;
        _executionQueue = executionQueue;
        _time = time;
        _info = info;
        _logger = logger;

        _lobby = new ValueProperty<LobbyState>(SharedLobbyService.LobbyPropertyId, new LobbyState
        {
            Code = info.Code,
            IsRunning = info.IsRunning
        });

        // One writer for every property: the start flag and the visibility - the client of the current leader (of
        // a lobby that has a start), the rest - only the backend (SetPropertyCommand drops the writes of anybody
        // else).
        _lobby.RestrictWriter(_ => false);
        _start.RestrictWriter(user => info.IsRunning == false && user == _owner.Owner.Value);
        _countdown.RestrictWriter(_ => false);
        _game.RestrictWriter(_ => false);
        _visibility.RestrictWriter(user => info.IsRunning == false && user == _owner.Owner.Value);

        BindProperty(_lobby);
        BindProperty(_start);
        BindProperty(_countdown);
        BindProperty(_game);
        BindProperty(_visibility);
    }

    public static readonly TimeSpan CountdownStep = TimeSpan.FromSeconds(1);

    private readonly ISessionOwner _owner;
    private readonly ISessionUsers _users;
    private readonly ISessionAdmission _admission;
    private readonly ILobbyMatches _matches;
    private readonly ILobbyListings _listings;
    private readonly IExecutionQueue _executionQueue;
    private readonly TimeProvider _time;
    private readonly LobbySessionInfo _info;
    private readonly ILogger<LobbyService> _logger;

    private readonly ValueProperty<LobbyState> _lobby;
    private readonly ValueProperty<LobbyStartState> _start = new(SharedLobbyService.StartPropertyId);
    private readonly ValueProperty<LobbyCountdownState> _countdown = new(SharedLobbyService.CountdownPropertyId);
    private readonly ValueProperty<LobbyGameState> _game = new(SharedLobbyService.GamePropertyId);
    private readonly ValueProperty<LobbyVisibilityState> _visibility = new(SharedLobbyService.VisibilityPropertyId);

    private ILifetime? _countdownLifetime;

    protected LobbySessionInfo Info => _info;
    protected ISessionUsers Users => _users;
    protected IExecutionQueue ExecutionQueue => _executionQueue;
    protected ILogger Logger => _logger;

    // The match is written to Game: the users are on their way to it.
    protected bool IsGameStarted => _game.Value.SessionId != Guid.Empty;

    // Running mode: the match has ended before the late players got in.
    protected bool IsRunOver => _lobby.Value.IsRunOver;

    protected override void OnStarted(IReadOnlyLifetime lifetime)
    {
        _logger.LogInformation("[Lobby] Lobby code {Code}", _lobby.Value.Code);

        _owner.Owner.View(lifetime, OnOwnerChanged);

        if (_info.IsRunning == true)
        {
            OnLatePlayersStarted(lifetime);
            return;
        }

        _start.Written.Advise(lifetime, OnStartWritten);
        _visibility.Written.Advise(lifetime, OnVisibilityWritten);

        // The list lifetime of a user ends after the user is removed from the list - or with the service (the
        // lifetime given by View is bounded by the subscriber one): the end of the session reports nothing.
        _users.View(lifetime, (userLifetime, _) => {
            ReportListing();

            userLifetime.Listen(() => {
                if (lifetime.IsTerminated == false)
                    ReportListing();
            });
        });
    }

    // The lobby of the late players of a running match is started: the game decides when its users get a place in the
    // match (Admit from the session queue, ClearGame when the entry is over, MarkRunOver when the match has ended).
    // By default nobody is admitted from the lobby: a player asks the meta for the match itself (SharedLobbies.JoinMatch).
    protected virtual void OnLatePlayersStarted(IReadOnlyLifetime lifetime)
    {
    }

    // Running mode, in the session queue: a place in the match for the user; the first place writes the match to Game.
    protected void Admit(Guid userId)
    {
        if (_info.IsRunning == false)
            throw new InvalidOperationException("Admit is for the lobby of the late players of a running match");

        var match = _matches.Admit(_info.RunningMatchId, userId);

        if (match == null)
        {
            _logger.LogWarning("{UserId} [Lobby] Match {SessionID} has no place for the late player",
                userId,
                _info.RunningMatchId);

            return;
        }

        if (IsGameStarted == true)
            return;

        _game.Set(new LobbyGameState
        {
            ServerUrl = match.ServerUrl,
            SessionId = match.SessionId,
            PlayersCount = 1
        });
    }

    // Running mode, in the session queue: the entry into the match is over, the users left in the lobby wait for the
    // next one.
    protected void ClearGame()
    {
        if (IsGameStarted == true)
            _game.Set(new LobbyGameState());
    }

    // Running mode, in the session queue: the match has ended (won, lost or closed) before the late players got in.
    protected void MarkRunOver()
    {
        _logger.LogInformation("[Lobby] Match {SessionID} is over: the late players go back", _info.RunningMatchId);

        _lobby.Update(state => state.IsRunOver = true);
        ClearGame();
    }

    private void OnOwnerChanged(IUser? owner)
    {
        // The countdown belongs to the leader that started it. After the match is created the users leave
        // the lobby one by one: nothing to reset.
        if (_start.Value.IsStarting == true && IsGameStarted == false)
        {
            _logger.LogInformation("[Lobby] The leader has changed during the countdown: the start is reset");

            StopCountdown();
            _start.Set(new LobbyStartState());
        }

        // Nobody is connected: the last index stays, nobody reads it; the next user to join becomes the leader.
        if (owner == null)
            return;

        _logger.LogInformation("[Lobby] Lobby leader: user {Index}", owner.Index);
        _lobby.Update(state => state.OwnerIndex = owner.Index);
        ReportListing();
    }

    private void OnVisibilityWritten(LobbyVisibilityState state)
    {
        _logger.LogInformation("[Lobby] Lobby {Code} is {Visibility}", _lobby.Value.Code, state.IsOpen ? "open" : "closed");
        ReportListing();
    }

    // From the start of the match on, the match reports the code.
    private void ReportListing()
    {
        if (_info.IsRunning == true || IsGameStarted == true)
            return;

        var owner = _owner.Owner.Value;

        _listings.Report(_info.SessionId, new LobbyListing
        {
            IsOpen = _visibility.Value.IsOpen,
            LeaderUserId = owner?.BackendId ?? Guid.Empty,
            PlayersCount = _users.CountPlayers(),
            MaxPlayers = _info.MaxUsers,
            IsRunning = false,
            IsJoinable = true
        });
    }

    private void OnStartWritten(LobbyStartState state)
    {
        // "Cancel" that came after zero: the match exists, the users are on their way to it.
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

        _logger.LogInformation("[Lobby] Start countdown: {Seconds} s", LobbyCountdownState.Seconds);

        _countdown.Set(new LobbyCountdownState
        {
            IsActive = true,
            SecondsLeft = LobbyCountdownState.Seconds
        });

        ScheduleStep(countdownLifetime);
    }

    private void StopCountdown()
    {
        if (_countdown.Value.IsActive == false)
            return;

        _logger.LogInformation("[Lobby] Start countdown is cancelled at {Seconds} s", _countdown.Value.SecondsLeft);

        _countdownLifetime!.Terminate();
        _countdownLifetime = null;

        _countdown.Set(new LobbyCountdownState());
    }

    private void ScheduleStep(IReadOnlyLifetime countdownLifetime)
    {
        Wait().NoAwait();

        async Task Wait()
        {
            // Cancelled with the countdown (a cancel, the end of the session): nothing waits for a dead step.
            await Task.Delay(CountdownStep, _time, countdownLifetime.Token).ConfigureAwait(false);
            _executionQueue.Enqueue(() => OnStep(countdownLifetime));
        }
    }

    private void OnStep(IReadOnlyLifetime countdownLifetime)
    {
        // cancelled (or the session is closed) while the step was waiting
        if (countdownLifetime.IsTerminated == true)
            return;

        var secondsLeft = _countdown.Value.SecondsLeft - 1;

        _countdown.Set(new LobbyCountdownState
        {
            IsActive = true,
            SecondsLeft = secondsLeft
        });

        if (secondsLeft > 0)
        {
            ScheduleStep(countdownLifetime);
            return;
        }

        StartGame();
    }

    private void StartGame()
    {
        _countdownLifetime!.Terminate();
        _countdownLifetime = null;

        // Sealed first: nobody can take a place in the lobby after the list of its users is read.
        _admission.Seal();

        // Everybody the lobby has let in: the connected users and those still on their way to it (a place
        // reserved by the code a moment ago) - the latter read the address as soon as they connect. A user is listed
        // once, whatever number of its connections are joined (a stale one is being dropped); a bot has no place.
        var userIds = _users.GetPlayerIds().ToList();

        foreach (var userId in _admission.GetPendingUsers(_time.GetUtcNow().UtcDateTime))
        {
            if (userIds.Contains(userId) == false)
                userIds.Add(userId);
        }

        var match = _matches.Create(_info.SessionId, userIds, _visibility.Value.IsOpen);

        _logger.LogInformation("[Lobby] Lobby {Code} starts the match {SessionID} for {Count} users",
            _lobby.Value.Code,
            match.SessionId,
            userIds.Count);

        _game.Set(new LobbyGameState
        {
            ServerUrl = match.ServerUrl,
            SessionId = match.SessionId,
            PlayersCount = userIds.Count
        });
    }
}
