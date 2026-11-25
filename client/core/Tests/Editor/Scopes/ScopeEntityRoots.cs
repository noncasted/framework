using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    public static class ScopeEntityRoots
    {
        public static void Entity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("entity", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
        }

        // A service that reacts to the end of the entity: it may still use the container and the assets.
        public static void EndingEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("ending", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityEndListener>().As<IScopeSetup>();
        }

        public static void FailingEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("failingEntity", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityThrowingSetup>().As<IScopeSetup>();
        }

        // The root of a game: it ends the entity through the lifetime it is given.
        public static void RootEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("root", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityRoot>().WithParameter(builder.ScopeLifetime).AsSelf();
        }

        // The build events wait: the lifetime may end while the assets of the entity load.
        public static void GatedEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("gated", builder.Events, builder.Lifetime);
            builder.Events.AddBeforeBuild(() => ScopeLoaderProbe.Gate.Task);
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
        }

        public static async UniTask AsyncGatedEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("asyncGated", builder.Events, builder.Lifetime);
            await ScopeLoaderProbe.Gate.Task;
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
        }
    }
}
