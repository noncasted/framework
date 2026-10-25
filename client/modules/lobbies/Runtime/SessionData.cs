using System;

namespace Global.Lobbies
{
    // The entry of the local user into a game session, as the meta answered it (SharedSessionRequests.Result).
    public class SessionData
    {
        public SessionData(
            string serverUrl,
            Guid sessionId,
            string ticket,
            string code,
            int expectedPlayers,
            bool isReturn = false,
            double reconnectStartedAt = double.NaN)
        {
            if (string.IsNullOrEmpty(ticket) == true)
                throw new ArgumentException($"[Matchmaking] Session {sessionId} without a ticket", nameof(ticket));

            ServerUrl = serverUrl;
            SessionId = sessionId;
            Ticket = ticket;
            Code = code;
            ExpectedPlayers = expectedPlayers;
            IsReturn = isReturn;
            ReconnectStartedAt = reconnectStartedAt;
        }

        public string ServerUrl { get; }
        public Guid SessionId { get; }

        // The entry of this user into the session: issued by the meta, valid for a minute, accepted once - a
        // session data is entered once (a rematch or a reconnect asks the meta for a new ticket).
        public string Ticket { get; }

        // Code of the lobby room; empty for a session that is not a lobby.
        public string Code { get; }

        // Players a match waits for before it starts: everybody its lobby has started it for. 1 for a match
        // found by the search: the others may join it at any moment, nobody is waited for.
        public int ExpectedPlayers { get; }

        // The entry of a client that has lost the connection to this match back into its seat (a new ticket of the
        // meta). A refusal of such an entry is not an error: the seat is gone (the reconnect window is over).
        public bool IsReturn { get; }

        // A return: when the reconnect that issued its ticket began (Time.realtimeSinceStartupAsDouble). A return that
        // fails on its entry goes on with that reconnect and its deadline. NaN for a first entry.
        public double ReconnectStartedAt { get; }

        // The same match entered again with a new ticket; nobody is waited for (the match is running).
        public SessionData ForReturn(string ticket, double reconnectStartedAt)
        {
            return new SessionData(ServerUrl, SessionId, ticket, Code, 1, true, reconnectStartedAt);
        }
    }
}
