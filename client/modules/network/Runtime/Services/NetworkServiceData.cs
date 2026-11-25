using System.Collections.Generic;

namespace Internal
{
    public class NetworkServiceData
    {
        public NetworkServiceData(
            int id,
            string key,
            IReadOnlyDictionary<int, INetworkProperty> properties,
            INetworkEvents events,
            IReadOnlyLifetime sessionLifetime)
        {
            Id = id;
            Key = key;
            Properties = properties;
            Events = events;
            SessionLifetime = sessionLifetime;
        }

        public int Id { get; }
        public string Key { get; }
        public IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
        public INetworkEvents Events { get; }
        public IReadOnlyLifetime SessionLifetime { get; }
    }
}
