using Shared;

namespace Internal
{
    public class RemoteUserUpdateCommand : OneWayCommand<SharedSessionPlayer.RemoteUpdate>
    {
        public RemoteUserUpdateCommand(
            INetworkUsersCollection users,
            INetworkSession session)
        {
            _users = users;
            _session = session;
        }

        private readonly INetworkUsersCollection _users;
        private readonly INetworkSession _session;

        // Peers are known by their index and id in this session only: the meta id of a user is not sent to others.
        // A known index: the user has returned to its seat - after RemoteAway, or before the server noticed its loss
        // (then it was never away here).
        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionPlayer.RemoteUpdate context)
        {
            if (_users.Entries.TryGetValue(context.Index, out var known) == true)
            {
                known.SetAway(false);
                return;
            }

            var user = new NetworkUser(context.Index, context.Id, false, _session.Lifetime.Child());
            _users.Add(user);
        }
    }
}