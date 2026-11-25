using System.Collections.Generic;

namespace Internal
{
    public interface INetworkService : INetworkObject
    {
        string Key { get; }
    }

    public class NetworkService : INetworkService
    {
        private NetworkServiceData _data;

        public int Id => _data.Id;
        public string Key => _data.Key;
        public IReadOnlyDictionary<int, INetworkProperty> Properties => _data.Properties;
        public INetworkEvents Events => _data.Events;

        public IReadOnlyLifetime Lifetime => _data.SessionLifetime;

        public void Start(NetworkServiceData data)
        {
            _data = data;
            OnStarted(Lifetime);
        }

        public virtual void OnStarted(IReadOnlyLifetime lifetime)
        {
        }
    }
}