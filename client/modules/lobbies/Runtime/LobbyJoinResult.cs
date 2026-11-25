using Shared;

namespace Global.Lobbies
{
    // Answer to IMatchmaking.CreateLobby / JoinLobby.
    public class LobbyJoinResult
    {
        public LobbyJoinResult(SharedSessionRequests.EntryStatus status, SessionData sessionData, string code)
        {
            Status = status;
            SessionData = sessionData;
            Code = code;
        }

        public SharedSessionRequests.EntryStatus Status { get; }

        // Null unless Status is Ok.
        public SessionData SessionData { get; }

        // The code of the room: the created one, or the one the join was requested with (normalized).
        public string Code { get; }
    }
}
