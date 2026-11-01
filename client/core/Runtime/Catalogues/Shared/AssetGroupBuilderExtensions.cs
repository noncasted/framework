using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class AssetGroupBuilderExtensions
    {
        // Groups are retained in one batch before the container is built, so each one
        // is measured by its own concurrent segment: otherwise the whole stage is one black box.
        public static IScopeBuilder RequestAssetGroup(this IScopeBuilder builder, AssetGroup group, string label)
        {
            // The release is registered by the retain: a Construct that fails before the build events has retained nothing.
            builder.Events.AddBeforeBuild(() =>
            {
                builder.Events.AddAfterDispose(() => Release(group));

                return GameProfiler
                       .Concurrent($"{label}: {group.Name}")
                       .Track(group.Retain());
            });

            return builder;
        }

        // Construct runs before the build events, so a group used while registering must be loaded right away.
        // The segment is concurrent: groups load in a batch via WhenAll, and a stacked
        // Scope would nest the siblings into each other instead of a flat list.
        // The release is registered before the load: a retain counts from the call, a failed one is released too.
        public static async UniTask LoadAssetGroup(this IScopeBuilder builder, AssetGroup group, string label)
        {
            builder.Events.AddAfterDispose(() => Release(group));

            await GameProfiler.Concurrent($"{label}: {group.Name}").Track(group.Retain());

            ContainerRegistryDebug.RecordLoadedAsset(builder.Lifetime, label, group.Name);
        }

        // Async: an error of one release is a failed task and the other callbacks of the pass still run.
        private static async UniTask Release(AssetGroup group)
        {
            await UniTask.CompletedTask;
            group.Release();
        }
    }
}