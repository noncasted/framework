using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ContentScopeExtensions
    {
        // The root scope of the game, once: the content directory of the build in the store of the platform (a player),
        // the assets of the project (Play Mode of the editor), and IContentLoader over it. The directory lives as long as
        // the root: its end destroys every object loaded from it. The generated catalogues load from it too (CatalogContent).
        public static async UniTask AddContent(this IScopeBuilder builder, ContentOptions options)
        {
            options.Validate();

            var loader = await CreateLoader(builder.Lifetime, options);

            builder.Content.Use(loader);
            await CatalogContent.Bind(builder.Lifetime, loader, loader.RootAssets<ContentRoot>());
            builder.RegisterInstance(loader).As<IContentLoader>();
        }

        // Construct runs before the build events, so an object used while registering is loaded right away. It is
        // released when the scope goes down; the load is a concurrent span of the loader ("Content: <type>").
        [ContainerPrimitive(ContainerPrimitive.Content)]
        public static async UniTask<T> LoadContent<T>(this IScopeBuilder builder, ContentReference<T> reference) where T : Object
        {
            var value = await builder.Content.Loader.Load(builder.Lifetime, reference);
            ContainerRegistryDebug.RecordLoadedAsset(builder.Lifetime, "Content", value.name);
            return value;
        }

        // Loaded in the batch before the build, in parallel with the asset groups and the scenes of the scope.
        [ContainerPrimitive(ContainerPrimitive.Content)]
        public static IScopeBuilder RequestContent<T>(this IScopeBuilder builder, ContentReference<T> reference, out ContentSlot<T> slot)
            where T : Object
        {
            var target = new ContentSlot<T>(reference);
            builder.Events.AddBeforeBuild(async () => target.Fill(await builder.LoadContent(reference)));
            slot = target;
            return builder;
        }

        private static async UniTask<ContentLoader> CreateLoader(IReadOnlyLifetime lifetime, ContentOptions options)
        {
#if UNITY_EDITOR
            await UniTask.CompletedTask;
            return new ContentLoader(EditorContentDirectories.Create(options.Name));
#else
            return await ContentLoader.Register(lifetime, options.Name, options.RemoteUrl);
#endif
        }
    }
}
