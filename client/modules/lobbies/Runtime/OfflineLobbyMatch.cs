using System;

namespace Global.Lobbies
{
    public readonly struct OfflineLobbyMatch
    {
        public OfflineLobbyMatch(string serverUrl, Guid sessionId)
        {
            ServerUrl = serverUrl;
            SessionId = sessionId;
        }

        public readonly string ServerUrl;
        public readonly Guid SessionId;
    }
}
