using Shared;

namespace Internal
{
    // the connection of a remote user is lost, its seat waits for the reconnect window. Its entities stay; it
    // returns with RemoteUpdate or leaves with RemoteDisconnect.
    public class RemoteUserAwayCommand : OneWayCommand<SharedSessionPlayer.RemoteAway>
    {
        public RemoteUserAwayCommand(INetworkUsersCollection users)
        {
            _users = users;
        }

        private readonly INetworkUsersCollection _users;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionPlayer.RemoteAway context)
        {
            _users.Entries[context.Index].SetAway(true);
        }
    }
}
