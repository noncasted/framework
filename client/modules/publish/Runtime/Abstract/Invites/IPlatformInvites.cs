using Internal;

namespace Global.Publisher
{
    // Invites of the friends of the platform into a lobby (Steam: the overlay invite dialog and "Join game" of the
    // friends list). The lobby is the code of the game (Shared.LobbyCode): the platform only carries it.
    // Steam only so far (Steam.SteamInvites, registered on every platform); elsewhere the game shows the code to share by hand.
    public interface IPlatformInvites
    {
        // False where the platform has no friends to invite (not Steam): nothing else may be called then.
        bool IsSupported { get; }

        // The code of a lobby the user asked to join from the platform (an accepted invite, "Join game", the launch
        // of the game by an invite); null when there is none. Stays until Consume: the menu joins it once it is on
        // its main screen.
        IViewableProperty<string> JoinRequest { get; }

        void ConsumeJoinRequest();

        // While the lifetime lasts the user is shown in the lobby: friends can join it from the platform.
        void ShowInLobby(IReadOnlyLifetime lifetime, string lobbyCode);

        // The dialog of the platform to pick the friends to invite.
        void OpenInviteDialog(string lobbyCode);
    }
}
