namespace Internal
{
    public static class UpdaterExtensions
    {
        // The updater of the global scope: one per game, its prefab belongs to the project.
        public static IScopeBuilder AddUpdater(this IScopeBuilder builder, Updater prefab)
        {
            var updater = builder.Instantiate(prefab);

            builder.RegisterComponent(updater)
                   .As<IUpdater>()
                   .AsSelfResolvable();

            return builder;
        }
    }
}
