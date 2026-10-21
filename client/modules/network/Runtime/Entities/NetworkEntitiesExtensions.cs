using Shared;

namespace Internal
{
    public static class NetworkEntitiesExtensions
    {
        // No WithParameter<int> here: the container generator merges parameters of the same type across
        // the whole entity scope, so every property and the entity itself received one shared int.
        public static IEntityBuilder RegisterProperty<T>(this IEntityBuilder builder) where T : new()
        {
            builder.Register<EntityNetworkProperty<T>>()
                   .As<INetworkProperty<T>>()
                   .As<INetworkProperty>()
                   .AsSelfResolvable();

            return builder;
        }
    }

    // Entity property id is derived from the value type: one property per type in an entity. The wire schema
    // (ProtocolVersion.Hash) pins the type names; NetworkEntity checks the ids of one entity are distinct.
    public class EntityNetworkProperty<T> : NetworkProperty<T> where T : new()
    {
        public EntityNetworkProperty() : base(StableHash.Get(typeof(T).FullName!))
        {
        }
    }
}
