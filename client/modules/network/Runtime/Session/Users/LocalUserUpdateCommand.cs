using System;
using Shared;

namespace Internal
{
    public class LocalUserUpdateCommand : OneWayCommand<SharedSessionPlayer.LocalUpdate>
    {
        public LocalUserUpdateCommand(
            INetworkUsersCollection users,
            INetworkSession session,
            INetworkEntityIds ids,
            INetworkEntityFactory factory)
        {
            _users = users;
            _session = session;
            _ids = ids;
            _factory = factory;
        }

        private readonly INetworkUsersCollection _users;
        private readonly INetworkSession _session;
        private readonly INetworkEntityIds _ids;
        private readonly INetworkEntityFactory _factory;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionPlayer.LocalUpdate context)
        {
            if (_users.Local != null)
                throw new InvalidOperationException($"[Network] A second LocalUpdate ({context.Index}) in one session");

            // A returning user goes on in its id range and restores its entities instead of creating them.
            _ids.Continue(context.EntityCounter);
            _factory.ExpectRestored(context.IsReturn, context.SeatEntities);

            var user = new NetworkUser(context.Index, context.Id, true, _session.Lifetime.Child());
            _users.Add(user);

            // Owner of the session entities (ISessionEntityPayload): they live while the session does.
            var session = new NetworkUser(NetworkUsersCollection.SessionIndex, Guid.Empty, false, _session.Lifetime.Child());
            _users.SetSession(session);
        }
    }
}