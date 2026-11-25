using System;
using Shared;

namespace Internal
{
    public class EntityDestroyedCommand : OneWayCommand<SharedSessionEntity.DestroyUpdate>
    {
        private readonly INetworkEntitiesCollection _entities;
        private readonly INetworkObjectsCollection _objects;
        private readonly INetworkEntityFactory _factory;
        private readonly ILog _log;

        public EntityDestroyedCommand(
            INetworkEntitiesCollection entities,
            INetworkObjectsCollection objects,
            INetworkEntityFactory factory,
            ILog<EntityDestroyedCommand> log)
        {
            _entities = entities;
            _objects = objects;
            _factory = factory;
            _log = log;
        }

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionEntity.DestroyUpdate context)
        {
            var id = context.EntityId;
            var state = _objects.GetState(id);

            switch (state)
            {
                case NetworkObjectState.Live:
                    _log.Debug("Destroying entity with ID: {Id}", id);
                    _entities.Entries[id].DestroyRemote();
                    break;

                // Not built yet: its listener still waits (a room object of a level that is not assembled,
                // destroyed with the floor the session has left).
                case NetworkObjectState.Creating:
                    if (_factory.TryCancelRemote(id) == false)
                        throw new InvalidOperationException($"[Network] Entity {id} is being created, but not by the factory");

                    _log.Debug("Entity with ID: {Id} is destroyed before it was built", id);
                    break;

                // Already gone here: destroyed with its owner, by this client (the floor it was on is unloaded) or
                // its creation failed.
                case NetworkObjectState.Destroyed:
                    _log.Debug("Entity with ID: {Id} is already destroyed here", id);
                    break;

                default:
                    throw new InvalidOperationException($"[Network] DestroyUpdate for entity {id} that this client has never had");
            }
        }
    }
}
