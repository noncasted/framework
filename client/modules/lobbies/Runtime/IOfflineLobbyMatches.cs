namespace Global.Lobbies
{
    // The match sessions of the offline local game server: created by the lobby of the offline mode when its countdown
    // reaches zero. Implemented by the game (its offline backend).
    public interface IOfflineLobbyMatches
    {
        OfflineLobbyMatch CreateMatch();
    }
}
