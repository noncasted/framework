using Cluster.Coordination;
using Cluster.Discovery;
using Game.Lobbies;
using Game.Session;
using Infrastructure;
using Internal;

namespace Game.Hosting;

// The search of the game: the session to enter with a place reserved for the user (see ISessionAdmission), a public
// match with a free place or the lobby of the late players of a running one (isLobby), otherwise a new public match.
// Lobbies by code and other private sessions are never found. Implemented by the game: it knows when a running match
// can be entered and builds the containers of its sessions.
public interface ISessionSearch
{
    Guid GetOrCreate(Guid userId, out bool isLobby);
}

// A match for a known set of users (a solo run): their places are reserved right away (an entry needs a live
// reservation), the session starts once all of them have joined, the search never finds it. Implemented by the game.
public interface IKnownUsersMatches
{
    ISession Create(IReadOnlyList<Guid> userIds);
}

// The handlers of the meta requests (SessionPayloads) on a game server, listening once the cluster is set up.
public class SessionEndpoints : ICoordinatorSetupCompleted
{
    public SessionEndpoints(
        IMessaging messaging,
        IKnownUsersMatches knownUsersMatches,
        ISessionSearch sessionSearch,
        ILobbySessions lobbySessions,
        IServiceDiscovery serviceDiscovery)
    {
        _messaging = messaging;
        _knownUsersMatches = knownUsersMatches;
        _sessionSearch = sessionSearch;
        _lobbySessions = lobbySessions;
        _serviceDiscovery = serviceDiscovery;
    }

    private readonly IMessaging _messaging;
    private readonly IKnownUsersMatches _knownUsersMatches;
    private readonly ISessionSearch _sessionSearch;
    private readonly ILobbySessions _lobbySessions;
    private readonly IServiceDiscovery _serviceDiscovery;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        Listen<SessionPayloads.Create.Request, SessionPayloads.Create.Response>(lifetime, Create);
        Listen<SessionPayloads.Search.Request, SessionPayloads.Search.Response>(lifetime, Search);
        Listen<SessionPayloads.LobbyCreate.Request, SessionPayloads.LobbyCreate.Response>(lifetime, CreateLobby);
        Listen<SessionPayloads.LobbyJoin.Request, SessionPayloads.LobbyJoin.Response>(lifetime, JoinLobby);
        Listen<SessionPayloads.MatchJoin.Request, SessionPayloads.MatchJoin.Response>(lifetime, JoinMatch);

        return Task.CompletedTask;
    }

    private void Listen<TRequest, TResponse>(IReadOnlyLifetime lifetime, Func<TRequest, Task<TResponse>> handler)
    {
        _messaging.AddPipeRequestHandler(
            lifetime,
            new MessagePipeServiceRequestId(_serviceDiscovery.Self, typeof(TRequest)),
            handler);
    }

    private Task<SessionPayloads.Create.Response> Create(SessionPayloads.Create.Request request)
    {
        var session = _knownUsersMatches.Create(request.UserIds);

        return Task.FromResult(new SessionPayloads.Create.Response
        {
            SessionId = session.Id
        });
    }

    private Task<SessionPayloads.Search.Response> Search(SessionPayloads.Search.Request request)
    {
        var id = _sessionSearch.GetOrCreate(request.UserId, out var isLobby);

        return Task.FromResult(new SessionPayloads.Search.Response
        {
            SessionId = id,
            IsLobby = isLobby
        });
    }

    private async Task<SessionPayloads.LobbyCreate.Response> CreateLobby(SessionPayloads.LobbyCreate.Request request)
    {
        var created = await _lobbySessions.Create(request.UserId);

        return new SessionPayloads.LobbyCreate.Response
        {
            SessionId = created.SessionId,
            Code = created.Code
        };
    }

    private Task<SessionPayloads.LobbyJoin.Response> JoinLobby(SessionPayloads.LobbyJoin.Request request)
    {
        var joined = _lobbySessions.Join(request.SessionId, request.MatchSessionId, request.Code, request.UserId);

        return Task.FromResult(new SessionPayloads.LobbyJoin.Response
        {
            Status = joined.Status,
            SessionId = joined.SessionId
        });
    }

    private Task<SessionPayloads.MatchJoin.Response> JoinMatch(SessionPayloads.MatchJoin.Request request)
    {
        var status = _lobbySessions.JoinMatch(request.LobbySessionId, request.MatchSessionId, request.UserId);

        return Task.FromResult(new SessionPayloads.MatchJoin.Response
        {
            Status = status
        });
    }
}
