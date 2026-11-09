namespace Internal
{
    public interface IScopeBuilder : IBuilder
    {
        ServiceScopeSceneLoader SceneLoader { get; }
        ScopeContent Content { get; }
        IServiceScopeBinder Binder { get; }
        ILifetime ScopeLifetime { get; }
        bool IsMock { get; }
    }

    public class ScopeBuilder : IScopeBuilder
    {
        public ScopeBuilder(
            ContainerBuilder containerBuilder,
            ServiceScopeSceneLoader sceneLoader,
            ScopeContent content,
            IServiceScopeBinder binder,
            ILifetime scopeLifetime,
            ILoadedScope parent,
            IEventLoop events,
            bool isMock)
        {
            ContainerBuilder = containerBuilder;
            SceneLoader = sceneLoader;
            Content = content;
            Binder = binder;
            ScopeLifetime = scopeLifetime;
            IsMock = isMock;
            Events = events;
            Parent = parent;

            containerBuilder.AttachBuilder(this);
        }

        public ContainerBuilder ContainerBuilder { get; }
        public ServiceScopeSceneLoader SceneLoader { get; }
        public ScopeContent Content { get; }
        public IServiceScopeBinder Binder { get; }
        public IContainerRegistry Registry => ContainerBuilder;
        public IEventLoop Events { get; }
        public IReadOnlyLifetime Lifetime => ScopeLifetime;
        public ILoadedScope Parent { get; }
        public ILifetime ScopeLifetime { get; }
        public bool IsMock { get; }
    }
}