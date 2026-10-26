using Cluster.Discovery;
using Cluster.Lobbies;
using Game.Hosting;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// Lobby sessions of this game server: private sessions entered by a code (or from the list of open lobbies,
// by the same code).
// A user holds a reserved place in one lobby of the server at a time: a place taken in another lobby (a new
// "Play", another code) drops the reservation in the previous one, and a lobby left with nobody is closed
// at once and frees its code. A repeated join of the same lobby only refreshes the reservation.
// The code of a lobby that has started its match leads to the lobby of the late players of that match (one per
// match, created by the first late player, closed when it is left with nobody): when the players get into the match
// is up to the game (ILobbyEntryPolicy, the LobbyService of the game). The search leads to the same lobby of a
// running public match (JoinLate), without a code.
public interface ILobbySessions
{
    // Creates a lobby session with a place reserved for the creator and a code registered for it.
    Task<LobbyCreated> Create(Guid userId);

    // Reserves a place in the lobby session the registry pointed to: the lobby itself while matchSessionId is
    // empty, otherwise the lobby of the late players of the match (Full when the match has no free place,
    // Started when it can't be entered now, NotFound when it is over).
    LobbyJoined Join(Guid lobbySessionId, Guid matchSessionId, string code, Guid userId);

    // A place in the running match of the lobby, for a late player: only when the game lets one in
    // (ILobbyEntryPolicy.CanEnterMatchNow, Started otherwise).
    LobbyJoinStatus JoinMatch(Guid lobbySessionId, Guid matchSessionId, Guid userId);

    // The search: a place in the lobby of the late players of a running public match (no code). Full when the
    // match has no free place, NotFound when it is over.
    LobbyJoined JoinLate(Guid matchSessionId, Guid userId);
}

public class LobbyJoined
{
    public required LobbyJoinStatus Status { get; init; }

    // Ok only: the session to enter (the lobby or the lobby of the late players).
    public Guid SessionId { get; init; }
}

public class LobbyCreated
{
    public required Guid SessionId { get; init; }
    public required string Code { get; init; }
}

public class LobbySessions : ILobbySessions
{
    public LobbySessions(
        ILobbyFactory factory,
        ISessionsCollection collection,
        ILobbyCodes codes,
        ILobbyCodeLeases codeLeases,
        ILobbyEntryPolicy entryPolicy,
        LobbyOptions options,
        IServiceDiscovery serviceDiscovery,
        ILogger<LobbySessions> logger)
    {
        _factory = factory;
        _collection = collection;
        _codes = codes;
        _codeLeases = codeLeases;
        _entryPolicy = entryPolicy;
        _maxUsers = options.MaxUsers;
        _serviceDiscovery = serviceDiscovery;
        _logger = logger;
    }

    private readonly ILobbyFactory _factory;
    private readonly ISessionsCollection _collection;
    private readonly ILobbyCodes _codes;
    private readonly ILobbyCodeLeases _codeLeases;
    private readonly ILobbyEntryPolicy _entryPolicy;
    private readonly int _maxUsers;
    private readonly IServiceDiscovery _serviceDiscovery;
    private readonly ILogger<LobbySessions> _logger;

    // The lobby each user has reserved a place in last; entries of a closed lobby are removed with it.
    private readonly Dictionary<Guid, ISession> _reserved = new();
    private readonly Lock _reservedLock = new();

    // Running match -> the lobby of its late players, while that lobby is alive.
    private readonly Dictionary<Guid, ISession> _runningLobbies = new();
    private readonly Lock _runningLock = new();

    public async Task<LobbyCreated> Create(Guid userId)
    {
        // The code is registered before the session exists: the session is built with its code. A join that
        // resolves the code in between finds no session and is answered NotFound.
        var sessionId = Guid.NewGuid();
        var code = await _codes.Register(_serviceDiscovery.Self.Id, sessionId);

        ISession session;

        try
        {
            session = _factory.Create(new LobbyCreation
            {
                Id = sessionId,
                Code = code,
                MaxUsers = _maxUsers
            });

            if (session.Id != sessionId || session.Type != SessionType.Lobby)
                throw new InvalidOperationException($"The lobby factory built {session.Type} session {session.Id} for lobby {sessionId}");
        }
        catch
        {
            // No lobby holds the code: nothing would ever release it.
            await _codes.Release(code, sessionId);
            throw;
        }

        // The code is released when the lobby is closed (the last user left or nobody entered in time) or,
        // if the lobby has started a match, when that match is closed.
        _codeLeases.Hold(code, session);
        session.Lifetime.Listen(() => Forget(session));

        if (session.TryReserve(userId) == false)
            throw new InvalidOperationException($"Created lobby session {sessionId} rejected the reservation");

        OnReserved(session, userId);

        _logger.LogInformation("{UserId} [Lobby] Lobby session {SessionID} created with code {Code}",
            userId,
            sessionId,
            code);

        return new LobbyCreated
        {
            SessionId = sessionId,
            Code = code
        };
    }

    public LobbyJoined Join(Guid lobbySessionId, Guid matchSessionId, string code, Guid userId)
    {
        if (matchSessionId != Guid.Empty)
            return JoinRunning(lobbySessionId, matchSessionId, code, userId);

        if (_collection.TryGet(lobbySessionId, out var session) == false || session.Type != SessionType.Lobby)
            return Refuse(LobbyJoinStatus.NotFound);

        if (session.TryReserve(userId) == true)
        {
            OnReserved(session, userId);

            return new LobbyJoined
            {
                Status = LobbyJoinStatus.Ok,
                SessionId = session.Id
            };
        }

        // Sealed at zero of the countdown: the match exists, but the registry has not been told yet - the next
        // join by the code gets to the match. Also for a sealed lobby its users have already left.
        if (session.IsSealed == true)
            return Refuse(LobbyJoinStatus.Started);

        // A closed session keeps its code until its lifetime is terminated from the session queue.
        if (session.IsClosed == true)
            return Refuse(LobbyJoinStatus.NotFound);

        return Refuse(LobbyJoinStatus.Full);
    }

    public LobbyJoinStatus JoinMatch(Guid lobbySessionId, Guid matchSessionId, Guid userId)
    {
        var status = CheckMatch(lobbySessionId, matchSessionId, userId, out var match);

        if (status != LobbyJoinStatus.Ok)
            return status;

        if (_entryPolicy.CanEnterMatchNow(matchSessionId) == false)
            return LobbyJoinStatus.Started;

        if (match!.TryReserve(userId) == true)
        {
            _logger.LogInformation("{UserId} [Lobby] A late player takes a place in match {SessionID}",
                userId,
                matchSessionId);

            return LobbyJoinStatus.Ok;
        }

        return match.IsClosed == true ? LobbyJoinStatus.NotFound : LobbyJoinStatus.Full;
    }

    public LobbyJoined JoinLate(Guid matchSessionId, Guid userId)
    {
        return JoinRunning(Guid.Empty, matchSessionId, string.Empty, userId);
    }

    // lobbySessionId empty: a public match of the search (no code).
    private LobbyJoined JoinRunning(Guid lobbySessionId, Guid matchSessionId, string code, Guid userId)
    {
        // Checked again when the match lets the player in: the lobby of the late players does not hold a place in
        // the match.
        var status = CheckMatch(lobbySessionId, matchSessionId, userId, out _);

        if (status != LobbyJoinStatus.Ok)
            return Refuse(status);

        // A lobby that is being closed is replaced: the reservation is refused only by a closed session.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var lobby = GetOrCreateRunningLobby(lobbySessionId, matchSessionId, code);

            if (lobby.TryReserve(userId) == true)
            {
                OnReserved(lobby, userId);

                return new LobbyJoined
                {
                    Status = LobbyJoinStatus.Ok,
                    SessionId = lobby.Id
                };
            }

            if (lobby.IsClosed == false)
                return Refuse(LobbyJoinStatus.Full);
        }

        throw new InvalidOperationException($"The lobby of the late players of match {matchSessionId} rejected the reservation");
    }

    private LobbyJoinStatus CheckMatch(Guid lobbySessionId, Guid matchSessionId, Guid userId, out ISession? match)
    {
        if (_collection.TryGet(matchSessionId, out match) == false ||
            match.Type != SessionType.Match ||
            match.IsClosed == true ||
            _entryPolicy.IsMatchOver(matchSessionId) == true)
            return LobbyJoinStatus.NotFound;

        if (lobbySessionId != Guid.Empty && _codeLeases.IsMatchJoinable(lobbySessionId) == false)
            return LobbyJoinStatus.Started;

        if (match.HasFreePlace(userId) == false)
            return LobbyJoinStatus.Full;

        return LobbyJoinStatus.Ok;
    }

    private ISession GetOrCreateRunningLobby(Guid lobbySessionId, Guid matchSessionId, string code)
    {
        lock (_runningLock)
        {
            if (_runningLobbies.TryGetValue(matchSessionId, out var existing) == true && existing.IsClosed == false)
                return existing;

            var lobby = _factory.Create(new LobbyCreation
            {
                Id = Guid.NewGuid(),
                Code = code,
                MaxUsers = _maxUsers,
                LobbySessionId = lobbySessionId,
                RunningMatchId = matchSessionId
            });

            if (lobby.Type != SessionType.Lobby)
                throw new InvalidOperationException($"The lobby factory built {lobby.Type} session {lobby.Id} for the late players of match {matchSessionId}");

            _runningLobbies[matchSessionId] = lobby;

            lobby.Lifetime.Listen(() => {
                Forget(lobby);

                lock (_runningLock)
                {
                    if (_runningLobbies.TryGetValue(matchSessionId, out var current) == true && current == lobby)
                        _runningLobbies.Remove(matchSessionId);
                }
            });

            _logger.LogInformation("[Lobby] Lobby {SessionID} of the late players of match {MatchSessionID} created",
                lobby.Id,
                matchSessionId);

            return lobby;
        }
    }

    private static LobbyJoined Refuse(LobbyJoinStatus status)
    {
        return new LobbyJoined
        {
            Status = status
        };
    }

    private void OnReserved(ISession session, Guid userId)
    {
        ISession? previous;

        lock (_reservedLock)
        {
            _reserved.TryGetValue(userId, out previous);
            _reserved[userId] = session;
        }

        if (previous == null || previous == session)
            return;

        _logger.LogInformation("{UserId} [Lobby] The place in lobby session {SessionID} is dropped: " +
                               "the user took a place in another lobby",
            userId,
            previous.Id);

        previous.CancelReservation(userId);
    }

    private void Forget(ISession session)
    {
        lock (_reservedLock)
        {
            foreach (var (userId, reserved) in _reserved)
            {
                if (reserved == session)
                    _reserved.Remove(userId);
            }
        }
    }
}
