namespace Global.Lobbies
{
    // Answer to IMatchmaking.SearchSession.
    public class SessionSearchResult
    {
        public SessionSearchResult(SessionData session, bool isLobby)
        {
            Session = session;
            IsLobby = isLobby;
        }

        public SessionData Session { get; }

        // The session is the lobby of the late players of a running match: the player enters the match from it
        // (LobbyState.IsRunning). Otherwise the match itself.
        public bool IsLobby { get; }
    }
}
