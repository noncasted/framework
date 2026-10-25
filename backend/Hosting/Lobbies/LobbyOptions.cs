namespace Game.Lobbies;

// The lobbies of a game server.
public class LobbyOptions
{
    // The places of a lobby and of the match it starts: the lobby screen and the match of the game are built for
    // that many.
    public required int MaxUsers { get; init; }
}

// What the lobby service of a session knows about its session: registered in the session container by the game
// (AddLobbyService) from the LobbyCreation its factory got.
public class LobbySessionInfo
{
    public required Guid SessionId { get; init; }

    // The code of the room; empty only for the lobby of the late players of a match found by the search.
    public required string Code { get; init; }

    // The lobby of the late players of a running match: the lobby session that holds the code (empty for a match
    // found by the search) and the match.
    public Guid LobbySessionId { get; init; }
    public Guid RunningMatchId { get; init; }

    public required int MaxUsers { get; init; }

    public bool IsRunning => RunningMatchId != Guid.Empty;
}

// What the listing service of a match started by a lobby knows about its match.
public class MatchListingInfo
{
    // The lobby session that holds the code (the key of the listing reports).
    public required Guid LobbySessionId { get; init; }

    // The lobby was open at the start: the match is listed too.
    public required bool IsOpen { get; init; }

    public required int MaxUsers { get; init; }
}
