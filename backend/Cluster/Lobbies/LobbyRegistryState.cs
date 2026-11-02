using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Lobbies;

[GenerateSerializer]
public class LobbyRegistryEntry
{
    [Id(0)] public Guid ServerId { get; set; }
    [Id(1)] public Guid SessionId { get; set; }

    // The match the lobby has started; empty while the lobby has not started. From that moment the code is
    // held for the match: a join by it leads to the lobby of the late players of the match.
    [Id(2)] public Guid MatchSessionId { get; set; }

    // What the "Connect" window shows of the lobby (or of its match), reported by the game server.
    [Id(3)] public LobbyListing Listing { get; set; } = new();
}

// The state of a lobby (or of the match it has started) for the list of open lobbies, reported by the game server.
[GenerateSerializer]
public class LobbyListing
{
    // The leader has opened the lobby (LobbyVisibilityState); a match keeps the value its lobby had at the start.
    [Id(0)] public bool IsOpen { get; set; }

    // The leader of the lobby / the owner of the match; empty while nobody is connected.
    [Id(1)] public Guid LeaderUserId { get; set; }

    [Id(2)] public int PlayersCount { get; set; }

    // The match of the lobby is going.
    [Id(3)] public bool IsRunning { get; set; }

    // False when the lobby can't be entered now: a lobby that is starting its match, a match that is ending.
    [Id(5)] public bool IsJoinable { get; set; }

    // The places of the lobby (and of its match): the game decides (LobbyOptions.MaxUsers).
    [Id(6)] public int MaxPlayers { get; set; }

    public bool IsListed => IsOpen == true &&
                            IsJoinable == true &&
                            PlayersCount > 0 &&
                            PlayersCount < MaxPlayers &&
                            LeaderUserId != Guid.Empty;
}

// The open lobbies of a deploy, pushed by the registry on OpenLobbiesChannelId after every change.
[GenerateSerializer]
public class OpenLobbies
{
    // Grows with every change: a listener drops a snapshot older than the one it has.
    [Id(0)] public long Version { get; init; }
    [Id(1)] public List<OpenLobby> Entries { get; init; } = new();
}

[GenerateSerializer]
public class OpenLobby
{
    [Id(0)] public string Code { get; init; } = string.Empty;
    [Id(1)] public Guid ServerId { get; init; }
    [Id(2)] public LobbyListing Listing { get; init; } = new();
}

[GenerateSerializer]
[GrainState(Table = "cluster", State = "lobby_registry", Lookup = "LobbyRegistry", Key = GrainKeyType.Guid)]
public class LobbyRegistryState : IDirectStateValue, IDeployScopedState
{
    // Code -> the lobby session that holds it (and the match of the lobby, once it has started).
    [Id(0)] public Dictionary<string, LobbyRegistryEntry> Entries { get; set; } = new();

    // Version of the last pushed OpenLobbies.
    [Id(1)] public long ListingVersion { get; set; }

    public int Version => 0;
}
