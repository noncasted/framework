using System;
using Internal;

namespace Global.Publisher
{
    public class NoPlatformInvites : IPlatformInvites
    {
        private readonly ViewableProperty<string> _joinRequest = new(null);

        public bool IsSupported => false;
        public IViewableProperty<string> JoinRequest => _joinRequest;

        public void ConsumeJoinRequest()
        {
            throw new InvalidOperationException("[Publisher] A join request is consumed on a platform without invites");
        }

        public void ShowInLobby(IReadOnlyLifetime lifetime, string lobbyCode)
        {
            throw new InvalidOperationException("[Publisher] A lobby is shown on a platform without invites");
        }

        public void OpenInviteDialog(string lobbyCode)
        {
            throw new InvalidOperationException("[Publisher] An invite dialog is opened on a platform without invites");
        }
    }
}
