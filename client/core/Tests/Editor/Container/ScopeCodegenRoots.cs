namespace Internal.Tests
{
    // Isolated root for scope-class emit tests. Complete graph, no holes.
    public static class ScopeCodegenRoots
    {
        [ContainerGraphRoot]
        public static void LoadGenerated(IScopeBuilder builder)
        {
            builder.Register<ScopeCodegenFirstSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenSecondSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenThirdSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenSingleton>();
            builder.Register<ScopeCodegenScoped>(ServiceLifetime.Scoped);
            builder.Register<ScopeCodegenTransient>(ServiceLifetime.Transient);
            builder.Register<ScopeCodegenBronze>().As<IScopeCodegenTier>();
            builder.Register<ScopeCodegenGold>().As<IScopeCodegenTier>();
            builder.Register<ScopeCodegenRow>();
        }
    }
}