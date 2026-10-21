using System;
using Shared;

namespace Internal
{
    public interface INetworkEntityIds
    {
        int GetEntityId();

        // the local user has returned to its seat, its range is used up to the counter (LocalUpdate): the next
        // ids go on after it. Only before the first id of this session.
        void Continue(int counter);
    }

    // Ids of the entities this client creates (its own and the session ones): the range of the local user
    // (SharedEntityIds), checked by the backend. A counter past the range is an exception, never an id of
    // another user.
    public class NetworkEntitiesIds : INetworkEntityIds
    {
        public NetworkEntitiesIds(INetworkSession session)
        {
            _session = session;
        }

        private readonly INetworkSession _session;

        private int _counter = 0;

        public void Continue(int counter)
        {
            if (_counter != 0)
                throw new InvalidOperationException($"[Network] The id counter is continued after {_counter} ids are given out");

            if (counter < 0 || counter >= SharedEntityIds.Stride)
                throw new ArgumentOutOfRangeException(nameof(counter), counter, "[Network] Entity counter is out of the id range");

            _counter = counter;
        }

        public int GetEntityId()
        {
            var userIndex = _session.LocalIndex();

            if (_counter >= SharedEntityIds.Stride - 1)
            {
                throw new InvalidOperationException(
                    $"[Network] User {userIndex} has created {_counter} entities: its id range is exhausted");
            }

            _counter++;

            return SharedEntityIds.Create(userIndex, _counter);
        }
    }
}
