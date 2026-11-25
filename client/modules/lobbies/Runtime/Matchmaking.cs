using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Lobbies
{
    public interface IMatchmaking
    {
        // Every request is a meta command answered by its result: a failed command throws NetworkRequestException,
        // no answer in the timeout of a request (20 s) - TimeoutException, a lost connection or the end of the
        // lifetime - OperationCanceledException. An Ok result carries the ticket of the local user into the session
        // (SessionData.Ticket).

        // Public match session with a free place in the queue of the game (0 by default; the queues never mix, the
        // game gives them their meaning), or the lobby of the late players of a running one
        // (SessionSearchResult.IsLobby: the match is entered from it).
        UniTask<SessionSearchResult> SearchSession(IReadOnlyLifetime lifetime, int queue = 0);

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

        private readonly IMetaSocket _socket;

        public async UniTask<SessionSearchResult> SearchSession(IReadOnlyLifetime lifetime, int queue = 0)
        {
            var result = await Request(lifetime, new SharedSessionRequests.Search { Queue = queue });

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

        // The timeout is the one of the writer (IConnectionWriter.WriteRequest). A shorter one of its own gave up
        // on a result the meta was still making: the lobby was created anyway, and a retry created a second one. The
        // end of the lifetime only stops the wait: the meta still makes the result, which the writer then reports as
        // a response to an unknown request.
        private UniTask<T> Request<T>(IReadOnlyLifetime lifetime, INetworkContext command)
        {
            return _socket.Connection.Writer
                          .WriteRequest<T>(command)
                          .AttachExternalCancellation(lifetime.Token);
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
