using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Lobbies
{
    // The socket of the meta the requests go through: the game registers its meta backend as this (the connection
    // is the current one, a reconnected socket is a new connection).
    public interface IMetaSocket
    {
        INetworkConnection Connection { get; }
    }

    public interface IMatchmaking
    {
        // Every request is a meta command answered by its result: a failed command throws NetworkRequestException,
        // no answer in 10 s - TimeoutException, a lost connection or the end of the lifetime -
        // OperationCanceledException. An Ok result carries the ticket of the local user into the session
        // (SessionData.Ticket).

        // Public match session with a free place, or the lobby of the late players of a running one
        // (SessionSearchResult.IsLobby: the match is entered from it).
        UniTask<SessionSearchResult> SearchSession(IReadOnlyLifetime lifetime);

        // A private match session for the local user alone (a solo run): nobody else finds or enters it.
        UniTask<SessionData> CreateSolo(IReadOnlyLifetime lifetime);

        // New private lobby session with a place for the local user.
        UniTask<LobbyJoinResult> CreateLobby(IReadOnlyLifetime lifetime);

        // Place in the lobby that holds the code; a refusal is a result (NotFound / Full / Started), not an error.
        UniTask<LobbyJoinResult> JoinLobby(IReadOnlyLifetime lifetime, string code);

        // A place in the running match of the code, from the lobby of its late players; a refusal is a result.
        UniTask<LobbyJoinResult> JoinMatch(IReadOnlyLifetime lifetime, string code);

        // A ticket into a session the local user holds a place in without a result of its own: the match its lobby
        // has started (LobbyGameState). The place itself is checked by the game server on the entry.
        UniTask<string> GetSessionTicket(IReadOnlyLifetime lifetime, Guid sessionId);
    }

    public class Matchmaking : IMatchmaking
    {
        public Matchmaking(IMetaSocket socket)
        {
            _socket = socket;
        }

        // The backend answers in tens of milliseconds; a result is awaited for less than the 20 s of a plain request:
        // the menu shows the failure sooner.
        private static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(10);

        private readonly IMetaSocket _socket;

        public async UniTask<SessionSearchResult> SearchSession(IReadOnlyLifetime lifetime)
        {
            var result = await Request(lifetime, new SharedSessionRequests.Search());

            if (result.Status != SharedSessionRequests.EntryStatus.Ok)
                throw new InvalidOperationException($"[Matchmaking] The search is refused: {result.Status}");

            var session = new SessionData(result.ServerUrl, result.SessionId, result.Ticket, string.Empty, 1);
            return new SessionSearchResult(session, result.IsLobby);
        }

        public async UniTask<SessionData> CreateSolo(IReadOnlyLifetime lifetime)
        {
            var result = await Request(lifetime, new SharedSessionRequests.CreateSolo());

            if (result.Status != SharedSessionRequests.EntryStatus.Ok)
                throw new InvalidOperationException($"[Matchmaking] The solo session is refused: {result.Status}");

            return new SessionData(result.ServerUrl, result.SessionId, result.Ticket, string.Empty, 1);
        }

        public async UniTask<LobbyJoinResult> CreateLobby(IReadOnlyLifetime lifetime)
        {
            return ToResult(await Request(lifetime, new SharedLobbies.CreateLobby()));
        }

        public async UniTask<LobbyJoinResult> JoinLobby(IReadOnlyLifetime lifetime, string code)
        {
            return ToResult(await Request(lifetime, new SharedLobbies.JoinLobby { Code = code }));
        }

        public async UniTask<LobbyJoinResult> JoinMatch(IReadOnlyLifetime lifetime, string code)
        {
            return ToResult(await Request(lifetime, new SharedLobbies.JoinMatch { Code = code }));
        }

        public async UniTask<string> GetSessionTicket(IReadOnlyLifetime lifetime, Guid sessionId)
        {
            var request = new SharedSessionRequests.TicketRequest { SessionId = sessionId };
            var response = await Request<SharedSessionRequests.TicketResponse>(lifetime, request);
            return response.Ticket;
        }

        private UniTask<SharedSessionRequests.Result> Request(IReadOnlyLifetime lifetime, INetworkContext command)
        {
            return Request<SharedSessionRequests.Result>(lifetime, command);
        }

        private async UniTask<T> Request<T>(IReadOnlyLifetime lifetime, INetworkContext command)
        {
            var requestLifetime = lifetime.Child();

            try
            {
                var response = _socket.Connection.Writer
                                      .WriteRequest<T>(command)
                                      .AttachExternalCancellation(requestLifetime.Token);

                var timeout = UniTask.Delay(ResultTimeout, DelayType.Realtime, cancellationToken: requestLifetime.Token);

                var (hasResult, result) = await UniTask.WhenAny(response, timeout);

                if (hasResult == false)
                {
                    throw new TimeoutException(
                        $"[Matchmaking] No {typeof(T).Name} in {ResultTimeout.TotalSeconds} s");
                }

                return result;
            }
            finally
            {
                requestLifetime.Terminate();
            }
        }

        private static LobbyJoinResult ToResult(SharedSessionRequests.Result result)
        {
            if (result.Status != SharedSessionRequests.EntryStatus.Ok)
                return new LobbyJoinResult(result.Status, null, result.Code);

            var sessionData = new SessionData(result.ServerUrl, result.SessionId, result.Ticket, result.Code, 1);
            return new LobbyJoinResult(result.Status, sessionData, result.Code);
        }
    }
}
