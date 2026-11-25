using System.Collections.Generic;

namespace Internal
{
    public interface INetworkEntitiesCollection
    {
        IReadOnlyDictionary<int, INetworkEntity> Entries { get; }

        void Add(INetworkEntity entity);
    }
}
