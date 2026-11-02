using System;
using Shared;
using UnityEngine;

namespace Internal
{
    public class EntityDestroyedCommand : OneWayCommand<SharedSessionEntity.DestroyUpdate>
    {
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkObjectsCollection _objects;
        private readonly INetworkEntityFactory _factory;

        public EntityDestroyedCommand(
            INetworkEntitiesCollection entities,
            INetworkObjectsCollection objects,
            INetworkEntityFactory factory)
        {
            _entities = entities;
            _objects = objects;
            _factory = factory;
        }

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionEntity.DestroyUpdate context)
        {
            var id = context.EntityId;
            var state = _objects.GetState(id);

            switch (state)
            {
                case NetworkObjectState.Live:
                    Debug.Log($"[Network] Destroying entity with ID: {id}");
                    _entities.Entries[id].DestroyRemote();
                    break;

                // Not built yet: its listener still waits (a room object of a level that is not assembled,
                // destroyed with the floor the session has left).
                case NetworkObjectState.Creating:
                    if (_factory.TryCancelRemote(id) == false)
                        throw new InvalidOperationException($"[Network] Entity {id} is being created, but not by the factory");

                    Debug.Log($"[Network] Entity with ID: {id} is destroyed before it was built");
                    break;

                // Already gone here: destroyed with its owner, by this client (the floor it was on is unloaded) or
                // its creation failed.
                case NetworkObjectState.Destroyed:
                    Debug.Log($"[Network] Entity with ID: {id} is already destroyed here");
                    break;

                default:
                    throw new InvalidOperationException($"[Network] DestroyUpdate for entity {id} that this client has never had");
            }
        }
    }
}
