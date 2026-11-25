using Cysharp.Threading.Tasks;

namespace Internal.Tests
{
    public static class EntityTeardownRoots
    {
        public static AssetGroup Group { get; private set; }
        public static ILifetime ParentLifetime { get; private set; }

        public static void SetGroup(AssetGroup group)
        {
            Group = group;
        }

        public static void SetParentLifetime(ILifetime lifetime)
        {
            ParentLifetime = lifetime;
        }

        public static void Request(IEntityBuilder builder)
        {
            RetainOnBuild(builder.Events);
        }

        public static UniTask RequestAsync(IEntityBuilder builder)
        {
            RetainOnBuild(builder.Events);
            return UniTask.CompletedTask;
        }

        public static async UniTask GatedLoad(IEntityBuilder builder)
        {
            await ScopeLoaderProbe.Gate.Task;
            await RetainNow(builder.Events);
        }

        public static void EndingSetup(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("endingSetup", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
            builder.Register<EntityTeardownEndingSetup>().As<IScopeSetup>();
        }

        // The catalogue extensions take an IScopeBuilder; an entity reaches the same events through its builder.
        private static void RetainOnBuild(IEventLoop events)
        {
            events.AddBeforeBuild(() =>
            {
                events.AddAfterDispose(Release);
                return Group.Retain();
            });
        }

        private static UniTask RetainNow(IEventLoop events)
        {
            events.AddAfterDispose(Release);
            return Group.Retain();
        }

        private static UniTask Release()
        {
            Group.Release();
            return UniTask.CompletedTask;
        }
    }
}
