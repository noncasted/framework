using Shared;

namespace Internal
{
    // Live object - the event is invoked; being created - it waits for the object; destroyed here while the event
    // was on its way (an enemy killed by the session owner the moment another client hit it) - dropped; an object
    // this client never had - an exception (INetworkObjectsCollection).
    public class EntityEventCommand : OneWayCommand<SharedSessionObject.Event>
    {
        public EntityEventCommand(INetworkObjectsCollection objects)
        {
            _objects = objects;
        }

        private readonly INetworkObjectsCollection _objects;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionObject.Event context)
        {
            _objects.ApplyEvent(context);
        }
    }
}
