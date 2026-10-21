using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class AssetGroupBuilderExtensions
    {
        // Groups are retained in one batch before the container is built, so each one
        // is measured by its own concurrent segment: otherwise the whole stage is one black box.
        public static IScopeBuilder RequestAssetGroup(this IScopeBuilder builder, AssetGroup group, string label)
        {
            builder.Events.AddBeforeBuild(() => GameProfiler
                                                .Concurrent($"{label}: {group.Name}")
                                                .Track(group.Retain()));
            builder.Events.AddAfterDispose(() => ReleaseAsync(group));
            return builder;
        }

        // Construct runs before the build events, so a group used while registering must be loaded right away.
        // The segment is concurrent: groups load in a batch via WhenAll, and a stacked
        // Scope would nest the siblings into each other instead of a flat list.
        public static async UniTask LoadAssetGroup(this IScopeBuilder builder, AssetGroup group, string label)
        {
            await GameProfiler.Concurrent($"{label}: {group.Name}").Track(group.Retain());

            builder.Events.AddAfterDispose(() => ReleaseAsync(group));
            ContainerRegistryDebug.RecordLoadedAsset(builder.Lifetime, label, group.Name);
        }

        private static UniTask ReleaseAsync(AssetGroup group)
        {
            group.Release();
            return UniTask.CompletedTask;
        }
    }
}