using System.Collections.Generic;
using Shared;

namespace Internal
{
    public class RemoteEntityData
    {
        public RemoteEntityData(
            INetworkUser owner,
            int id,
            IReadOnlyList<SharedSessionObject.PropertyUpdate> rawProperties,
            IEntityPayload payload)
        {
            Owner = owner;
            Id = id;
            RawProperties = rawProperties;
            Payload = payload;
        }

        public INetworkUser Owner { get; }
        public int Id { get; }
        public IReadOnlyList<SharedSessionObject.PropertyUpdate> RawProperties { get; }
        public IEntityPayload Payload { get; }
    }
}
