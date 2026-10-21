namespace Global.Publisher.Steam
{
    // The Steam settings of a game, given to AddSteam.
    public class SteamOptions
    {
        public SteamOptions(uint appId, string webApiIdentity, string inviteConnectKey)
        {
            AppId = appId;
            WebApiIdentity = webApiIdentity;
            InviteConnectKey = inviteConnectKey;
        }

        // The app of the game in Steamworks. The editor and a build run outside Steam read it from steam_appid.txt
        // next to the working directory (client/steam_appid.txt for the editor).
        public uint AppId { get; }

        // The identity of the Web API ticket: the server of the game checks the ticket with the same one.
        public string WebApiIdentity { get; }

        // The key of the connect string of an invite into a lobby: "<key> <lobby code>" in the rich presence and in
        // the command line of a launch by an invite (e.g. "+game_lobby").
        public string InviteConnectKey { get; }
    }
}
