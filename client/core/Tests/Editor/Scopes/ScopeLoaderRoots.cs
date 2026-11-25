using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    public static class ScopeLoaderRoots
    {
        [ContainerGraphRoot]
        public static UniTask FailingSetup(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("failing", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderThrowingSetup>().As<IScopeSetup>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask Parent(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("parent", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderParentTeardown>().As<IScopeSetup>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(ScopeLoaderRoots), nameof(Parent))]
        public static UniTask Child(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("child", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderChildTeardown>().As<IScopeSetup>();
            return UniTask.CompletedTask;
        }
    }
}
