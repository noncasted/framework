using Cluster.Discovery;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Cluster.Lobbies;

// Registry "lobby code -> game server + session" of one deploy (the key is the DeployId, like
// IServiceDiscoveryStorage): game servers register their lobby sessions here, the meta resolves a code typed
// by a user. One grain per deploy makes "the code is unique among the live lobbies" a single-threaded check;
// the state is persistent, so an idle registry is not lost with the grain activation, and the records of
// old deploys are removed by IDeployCleanup.
public interface ILobbyRegistry : IGrainWithGuidKey
{
    // Returns a new code that no other registered lobby holds.
    Task<string> Register(Guid serverId, Guid sessionId);

    Task<LobbyRegistryEntry?> Find(string code);

    // The lobby has started its match: the code stays taken (it is never given to a new lobby) until it is
    // released, the entry gets the match session.
    Task MarkStarted(string code, Guid sessionId, Guid matchSessionId);

    // The session is passed to keep a late release from removing a code that already belongs to another lobby.
    // A started lobby releases its code with the same (lobby) session id when the match is closed.
    Task Release(string code, Guid sessionId);

    // What the lobby of the code (or its match) shows in the list of open lobbies. Reported by the game server
    // of the lobby, in order with the calls above for the same code (ILobbyCodeLeases). The session is the lobby
    // session that holds the code, for its match too.
    Task UpdateListing(string code, Guid sessionId, LobbyListing listing);

    // The current open lobbies; the changes are pushed on OpenLobbiesChannelId(deploy id).
    Task<OpenLobbies> GetOpenLobbies();
}

public class LobbyRegistry : Grain, ILobbyRegistry
{
    public LobbyRegistry(
        [State] State<LobbyRegistryState> state,
        IMessaging messaging,
        ILogger<LobbyRegistry> logger)
    {
        _state = state;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly State<LobbyRegistryState> _state;
    private readonly IMessaging _messaging;
    private readonly ILogger<LobbyRegistry> _logger;

    public async Task<string> Register(Guid serverId, Guid sessionId)
    {
        var code = string.Empty;

        await _state.Write(state => {
            code = LobbyCodeGenerator.GenerateUnique(state.Entries.ContainsKey);

            state.Add(code, new LobbyRegistryEntry
            {
                ServerId = serverId,
                SessionId = sessionId
            });
        });

        _logger.LogInformation("[LobbyRegistry] Code {Code} registered for session {SessionId} on server {ServerId}",
            code,
            sessionId,
            serverId);

        return code;
    }

    public async Task<LobbyRegistryEntry?> Find(string code)
    {
        var state = await _state.ReadValue();
        return state.Entries.GetValueOrDefault(code);
    }

    public async Task MarkStarted(string code, Guid sessionId, Guid matchSessionId)
    {
        if (matchSessionId == Guid.Empty)
            throw new ArgumentException("The match session of a started lobby is empty", nameof(matchSessionId));

        var state = await _state.ReadValue();

        if (state.Entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
            throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

        if (entry.MatchSessionId != Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Lobby code {code} is already marked as started with match {entry.MatchSessionId}");
        }

        await _state.Write(value => value.Entries[code].MarkStarted(matchSessionId));

        _logger.LogInformation("[LobbyRegistry] Code {Code} of session {SessionId} is held for match {MatchSessionId}",
            code,
            sessionId,
            matchSessionId);
    }

    public async Task Release(string code, Guid sessionId)
    {
        var state = await _state.ReadValue();

        if (state.Entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
            throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

        var wasListed = entry.Listing.IsListed;

        await _state.Write(value => {
            value.Remove(code);

            if (wasListed == true)
                value.AdvanceListingVersion();
        });

        _logger.LogInformation("[LobbyRegistry] Code {Code} released by session {SessionId}", code, sessionId);

        if (wasListed == true)
            await PublishOpenLobbies();
    }

    public async Task UpdateListing(string code, Guid sessionId, LobbyListing listing)
    {
        var state = await _state.ReadValue();

        if (state.Entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
            throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

        // Only a change of the list itself is pushed: the players of a closed lobby come and go unseen.
        var isListChanged = entry.Listing.IsListed == true || listing.IsListed == true;

        await _state.Write(value => {
            value.Entries[code].UpdateListing(listing);

            if (isListChanged == true)
                value.AdvanceListingVersion();
        });

        if (isListChanged == true)
            await PublishOpenLobbies();
    }

    public Task<OpenLobbies> GetOpenLobbies()
    {
        return CreateOpenLobbies();
    }

    private async Task PublishOpenLobbies()
    {
        var lobbies = await CreateOpenLobbies();

        await _messaging.PublishChannel(new OpenLobbiesChannelId(this.GetPrimaryKey()), lobbies);
    }

    private async Task<OpenLobbies> CreateOpenLobbies()
    {
        var state = await _state.ReadValue();

        // The entries of a game server that stopped without releasing them (a crash) stay until the end of the
        // deploy: a lobby is listed only while its server is alive in the discovery of the deploy.
        var members = await GrainFactory.GetGrain<IServiceDiscoveryStorage>(this.GetPrimaryKey()).Get();
        var aliveSince = DateTime.UtcNow - ServiceDiscoveryStorage.StaleThreshold;

        var lobbies = new OpenLobbies
        {
            Version = state.ListingVersion
        };

        foreach (var (code, entry) in state.Entries)
        {
            if (entry.Listing.IsListed == false)
                continue;

            if (members.TryGetValue(entry.ServerId, out var server) == false || server.UpdateTime < aliveSince)
                continue;

            lobbies.Entries.Add(new OpenLobby
            {
                Code = code,
                ServerId = entry.ServerId,
                Listing = entry.Listing
            });
        }

        return lobbies;
    }
}
