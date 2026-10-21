using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public class NetworkEntityFactory : INetworkEntityFactory
    {
        public NetworkEntityFactory(
            INetworkConnection connection,
            INetworkEntitiesCollection entities,
            INetworkObjectsCollection objects,
            INetworkUsersCollection users,
            INetworkEntityIds ids)
        {
            _connection = connection;
            _entities = entities;
            _objects = objects;
            _users = users;
            Ids = ids;
        }

        private readonly INetworkConnection _connection;
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkObjectsCollection _objects;
        private readonly INetworkUsersCollection _users;

        private readonly Dictionary<Type, Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>>>
            _listeners = new();

        private readonly Dictionary<Type, Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>>>
            _restoreListeners = new();

        private bool _isRestoreExpected;
        private int _restoreExpected;
        private int _restoreReceived;

        // Remote entities whose listeners are still building them, by the entity id.
        private readonly Dictionary<int, ILifetime> _creating = new();

        public INetworkUser LocalUser => _users.Local;
        public INetworkUser SessionUser => _users.Session;
        public INetworkEntityIds Ids { get; }
        public bool IsReturn { get; private set; }

        public void ListenRemote<T>(
            IReadOnlyLifetime lifetime,
            Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>> listener)
        {
            var type = typeof(T);
            _listeners.Add(type, listener);
            lifetime.Listen(() => _listeners.Remove(type));
        }

        public void ListenRestored<T>(
            IReadOnlyLifetime lifetime,
            Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>> listener)
        {
            var type = typeof(T);
            _restoreListeners.Add(type, listener);
            lifetime.Listen(() => _restoreListeners.Remove(type));
        }

        public void ExpectRestored(bool isReturn, int count)
        {
            if (_isRestoreExpected == true)
                throw new InvalidOperationException("[Network] The restore of the seat is expected twice");

            if (count < 0 || (isReturn == false && count != 0))
                throw new ArgumentOutOfRangeException(nameof(count), count, $"[Network] Seat entities of a user, return: {isReturn}");

            _isRestoreExpected = true;
            IsReturn = isReturn;
            _restoreExpected = count;
        }

        public UniTask WaitRestoredReceived(IReadOnlyLifetime lifetime)
        {
            if (_isRestoreExpected == false)
                throw new InvalidOperationException("[Network] The restore is awaited before the local user data");

            if (_restoreReceived >= _restoreExpected)
                return UniTask.CompletedTask;

            return UniTask.WaitUntil(() => _restoreReceived >= _restoreExpected, cancellationToken: lifetime.Token);
        }

        public async UniTask Send(IReadOnlyLifetime lifetime, INetworkEntity entity, IEntityPayload payload)
        {
            var properties = new List<SharedSessionObject.PropertyUpdate>();

            foreach (var (id, property) in entity.Properties)
            {
                properties.Add(new SharedSessionObject.PropertyUpdate()
                {
                    ObjectId = entity.Id,
                    PropertyId = id,
                    Value = property.Collect()
                });
            }

            var request = new SharedSessionEntity.CreateRequest()
            {
                Id = entity.Id,
                Properties = properties,
                Payload = payload
            };

            await _connection.Request<SharedSessionEntity.CreateResponse>(request);

            // Destroyed while the request was on its way: the id is only marked destroyed.
            _objects.Add(entity);

            if (entity.Lifetime.IsTerminated == false)
                _entities.Add(entity);
        }

        public async UniTask CreateRemote(IReadOnlyLifetime lifetime, RemoteEntityData data)
        {
            // An entity of the local user to restore: one its client has created before a reconnect. One built by the
            // backend for it (its id is issued by the backend) is received as any remote entity.
            var isRestore = data.Owner.IsLocal == true && SharedEntityIds.IsBackendIssued(data.Id) == false;

            // Synchronously too: WaitRestoredReceived counts the entities of the returned seat as they arrive.
            if (isRestore == true)
            {
                _restoreReceived++;

                if (IsReturn == false || _restoreReceived > _restoreExpected)
                {
                    throw new InvalidOperationException(
                        $"[Network] Entity {data.Id} of the local user is received, return: {IsReturn}, " +
                        $"{_restoreReceived} of {_restoreExpected} expected");
                }
            }

            // Synchronously, in the command that received CreatedOverview: the updates and events that follow it
            // are buffered until the listener has built the entity (INetworkObjectsCollection).
            _objects.BeginCreation(data.Id);

            // The lifetime of the creation: ends when the entity is destroyed - while its listener still builds it
            // too (TryCancelRemote: a room object that waits for its level), so the listener stops waiting.
            var creation = lifetime.Child();
            _creating.Add(data.Id, creation);

            INetworkEntity entity;

            try
            {
                await UniTask.SwitchToMainThread();

                var listeners = isRestore == true ? _restoreListeners : _listeners;

                if (listeners.TryGetValue(data.Payload.GetType(), out var listener) == false)
                {
                    throw new InvalidOperationException(
                        $"[Network] No {(isRestore ? "restore " : string.Empty)}listener for entity {data.Id} " +
                        $"with payload {data.Payload.GetType().Name}");
                }

                entity = await listener.Invoke(creation, data);
            }
            catch
            {
                // Cancelled (TryCancelRemote, the end of the session) or failed: whatever the listener has built on
                // the lifetime of the creation goes with it, the lifetime doesn't stay a child of the session. The
                // id becomes destroyed: a failure is reported once (by the caller), not by every later update of
                // it. The exception itself goes on: a cancellation is not an error.
                creation.Terminate();
                _objects.CancelCreation(data.Id);
                throw;
            }
            finally
            {
                _creating.Remove(data.Id);
            }

            // Destroyed while the listener was finishing: the entity it has built is not of the session any more.
            if (creation.IsTerminated == true)
            {
                entity.DestroyRemote();
                _objects.CancelCreation(data.Id);
                return;
            }

            // Its owner left while the listener was building it: the entity has died with the owner.
            if (entity.Lifetime.IsTerminated == true)
            {
                creation.Terminate();
                _objects.CancelCreation(data.Id);
                return;
            }

            entity.Lifetime.Listen(creation.Terminate);
            _entities.Add(entity);

            // Applies what came while the listener was building it.
            _objects.Add(entity);
        }

        public bool TryCancelRemote(int entityId)
        {
            if (_creating.TryGetValue(entityId, out var creation) == false)
                return false;

            _objects.CancelCreation(entityId);
            creation.Terminate();
            return true;
        }
    }
}