using System.Collections.Generic;

namespace Internal
{
    public interface INetworkUsersCollection
    {
        // Connected users of the session (the local one too), without the session "user".
        IReadOnlyDictionary<int, INetworkUser> Entries { get; }
        INetworkUser Local { get; }

        // Owner of the session entities (ISessionEntityPayload), index SessionIndex: lives until the session ends.
        INetworkUser Session { get; }

        void Add(INetworkUser user);
        void SetSession(INetworkUser session);

        // The owner of an entity by the OwnerId of CreatedOverview: a user or the session.
        INetworkUser GetOwner(int index);
    }

    public class NetworkUsersCollection : INetworkUsersCollection
    {
        // Owner index of the session entities in CreatedOverview (backend Entity.SessionOwnerIndex): user indices start
        // from 1, the session "user" is created together with the local one and is not in Entries.
        public const int SessionIndex = 0;

        private readonly Dictionary<int, INetworkUser> _entries = new();

        private INetworkUser _local;
        private INetworkUser _session;

        public IReadOnlyDictionary<int, INetworkUser> Entries => _entries;
        public INetworkUser Local => _local;
        public INetworkUser Session => _session;

        public void SetSession(INetworkUser session)
        {
            _session = session;
            session.Lifetime.Listen(() => _session = null);
        }

        public INetworkUser GetOwner(int index)
        {
            return index == SessionIndex ? _session : _entries[index];
        }

        public void Add(INetworkUser user)
        {
            if (user.IsLocal == true)
                _local = user;

            _entries.Add(user.Index, user);
            user.Lifetime.Listen(() => _entries.Remove(user.Index));
        }
    }
}