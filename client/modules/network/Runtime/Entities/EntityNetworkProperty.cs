using Shared;

namespace Internal
{
    // Entity property id is derived from the value type: one property per type in an entity. The wire schema
    // (ProtocolVersion.Hash) pins the type names; NetworkEntity checks the ids of one entity are distinct.
    public class EntityNetworkProperty<T> : NetworkProperty<T> where T : new()
    {
        public EntityNetworkProperty() : base(StableHash.Get(typeof(T).FullName!))
        {
        }
    }
}
