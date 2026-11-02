using Shared;

namespace Internal
{
    // Live object - applied if newer than its value; being created - the newest one waits for the object;
    // destroyed - dropped; an object this client never had or a property it doesn't have - an exception
    // (INetworkObjectsCollection).
    public class EntityPropertyUpdateCommand : OneWayCommand<SharedSessionObject.PropertyUpdate>
    {
        public EntityPropertyUpdateCommand(INetworkObjectsCollection objects)
        {
            _objects = objects;
        }

        private readonly INetworkObjectsCollection _objects;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedSessionObject.PropertyUpdate context)
        {
            _objects.ApplyUpdate(context);
        }
    }
}
