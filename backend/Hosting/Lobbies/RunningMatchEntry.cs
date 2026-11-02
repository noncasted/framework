using Game.Hosting;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// The way of a late player into a running match: a place in the lobby of its late players, or in the match itself when
// the game lets one in.
internal class RunningMatchEntry
{
    public RunningMatchEntry(
        ISessionsCollection collection,
        ILobbyCodeLeases codeLeases,
        ILobbyEntryPolicy entryPolicy,
        RunningLobbies lobbies,
        LobbyReservations reservations,
        ILogger logger)
    {
        _collection = collection;
        _codeLeases = codeLeases;
        _entryPolicy = entryPolicy;
        _lobbies = lobbies;
        _reservations = reservations;
        _logger = logger;
    }

    private readonly ISessionsCollection _collection;
    private readonly ILobbyCodeLeases _codeLeases;
    private readonly ILobbyEntryPolicy _entryPolicy;
    private readonly RunningLobbies _lobbies;
    private readonly LobbyReservations _reservations;
    private readonly ILogger _logger;

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

    // lobbySessionId empty: a public match of the search (no code).
    public LobbyJoined JoinLobby(Guid lobbySessionId, Guid matchSessionId, string code, Guid userId)
    {
        // Checked again when the match lets the player in: the lobby of the late players does not hold a place in
        // the match.
        var status = CheckMatch(lobbySessionId, matchSessionId, userId, out _);

        if (status != LobbyJoinStatus.Ok)
            return LobbyJoined.Refused(status);

        // A lobby that is being closed is replaced: the reservation is refused only by a closed session.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var lobby = _lobbies.GetOrCreate(lobbySessionId, matchSessionId, code);

            if (lobby.TryReserve(userId) == true)
            {
                _reservations.OnReserved(lobby, userId);

                return new LobbyJoined
                {
                    Status = LobbyJoinStatus.Ok,
                    SessionId = lobby.Id
                };
            }

            if (lobby.IsClosed == false)
                return LobbyJoined.Refused(LobbyJoinStatus.Full);
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
}
