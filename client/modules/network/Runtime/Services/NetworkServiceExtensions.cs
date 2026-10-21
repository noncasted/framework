using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public static class NetworkServiceExtensions
    {
        // Fetches a service from the session (GetRequest), fills its properties and starts it. Each network
        // service creates one with its own property ids (SharedLobbyService, SharedLocationService).
        public class NetworkServiceResolver<T> : INetworkSessionSetupCompleted where T : NetworkService
        {
            public NetworkServiceResolver(
                INetworkObjectsCollection objectsCollection,
                INetworkConnection connection,
                IReadOnlyDictionary<int, INetworkProperty> properties,
                T service,
                string key)
            {
                _objectsCollection = objectsCollection;
                _connection = connection;
                _properties = properties;
                _service = service;
                _key = key;
            }

            private readonly INetworkObjectsCollection _objectsCollection;
            private readonly INetworkConnection _connection;
            private readonly IReadOnlyDictionary<int, INetworkProperty> _properties;
            private readonly T _service;
            private readonly string _key;

            public async UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime)
            {
                var propertiesIds = _properties.Select(x => x.Key).ToList();

                var request = new SharedSessionService.GetRequest()
                {
                    Key = _key,
                    PropertiesIds = propertiesIds
                };

                var response = await _connection.Request<SharedSessionService.GetResponse>(request);

                for (var index = 0; index < response.Properties.Count; index++)
                {
                    var receivedProperty = response.Properties[index];
                    var targetProperty = _properties[receivedProperty.PropertyId];

                    if (receivedProperty.Value.Length != 0)
                        targetProperty.Update(receivedProperty.Value, receivedProperty.Version);
                }

                var events = new NetworkEvents(_connection, _service);

                var data = new NetworkServiceData(
                    response.Id,
                    _key,
                    _properties,
                    events,
                    lifetime);

                _service.Start(data);
                _objectsCollection.Add(_service);
            }
        }
    }
}