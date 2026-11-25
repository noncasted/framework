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
}
