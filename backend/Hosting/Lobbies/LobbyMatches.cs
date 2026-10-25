using Cluster.Lobbies;
using Game.Session;

namespace Game.Lobbies;

// What a lobby session needs from its game server when the start countdown reaches zero: a match session
// with a place for every user of the lobby. Implemented by the game (it builds the match with its options and passes
// the code to the match: ILobbyCodeLeases.PassToMatch).
public interface ILobbyMatches
{
    // Private match on this server, a place is reserved for each of the users (SessionAdmission.JoinTimeout).
    // The code of the lobby passes to the match: it stays taken until the match is closed.
    // isOpen: the lobby is listed among the open lobbies at the start - so is its match.
    LobbyMatch Create(Guid lobbySessionId, IReadOnlyList<Guid> userIds, bool isOpen);

    // The lobby of the late players of a running match on this server: a place is reserved for the user
    // (SessionAdmission.JoinTimeout). Null when the match is closed or has no free place.
    LobbyMatch? Admit(Guid matchSessionId, Guid userId);
}

// The list of open lobbies, fed by the sessions of a lobby code: the lobby reports itself until it starts, then its
// match does (by the id of the lobby session that holds the code). The reports of one code go to the registry in
// order, only the latest of a burst (LobbyCodeLeases).
public interface ILobbyListings
{
    void Report(Guid lobbySessionId, LobbyListing listing);
}

public class LobbyMatch
{
    public required string ServerUrl { get; init; }
    public required Guid SessionId { get; init; }
}

// Creates the lobby sessions of this game server: a session of type Lobby with the places of LobbyCreation, private,
// nobody expected (ExpectedUsers 0), with a LobbyService (AddLobbyService) in its container. Implemented by the game.
public interface ILobbyFactory
{
    ISession Create(LobbyCreation creation);
}

public class LobbyCreation
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required int MaxUsers { get; init; }

    // The lobby of the late players of a running match (see LobbySessionInfo).
    public Guid LobbySessionId { get; init; }
    public Guid RunningMatchId { get; init; }
}

// When a running match lets a new player in, decided by the game (the match may be entered only at a rest, never
// after its finale): asked by the lobby sessions before a place in the match is reserved.
public interface ILobbyEntryPolicy
{
    // The match has ended: the lobby of its late players goes back, the code answers NotFound.
    bool IsMatchOver(Guid matchSessionId);

    // A late player may take a place in the match now (a join by its code is answered Started otherwise).
    bool CanEnterMatchNow(Guid matchSessionId);
}

public static class LobbyEntryPolicy
{
    // Every running match can be entered at any time until it is closed.
    public sealed class Always : ILobbyEntryPolicy
    {
        public bool IsMatchOver(Guid matchSessionId)
        {
            return false;
        }

        public bool CanEnterMatchNow(Guid matchSessionId)
        {
            return true;
        }
    }
}
