namespace Internal
{
    /// <summary>
    /// Builder of the application root scope: no scene and no parent, registrations only.
    /// </summary>
    public class RootBuilder : IBuilder
    {
        public RootBuilder(ContainerBuilder containerBuilder, IEventLoop events, IReadOnlyLifetime lifetime)
        {
            ContainerBuilder = containerBuilder;
            Events = events;
            Lifetime = lifetime;

            containerBuilder.AttachBuilder(this);
        }

        public ContainerBuilder ContainerBuilder { get; }
        public IContainerRegistry Registry => ContainerBuilder;
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}