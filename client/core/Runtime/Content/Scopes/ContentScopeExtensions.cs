using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ContentScopeExtensions
    {
        // The root scope of the game, once: the catalog of the bundles of the build and the store of the platform (a
        // player), the assets of the project (Play Mode of the editor); the main bundle (options.Name) loaded whole and held
        // as long as the root, IContentLoader over its roots. The generated catalogues load from it too (CatalogContent).
        public static async UniTask AddContent(this IScopeBuilder builder, ContentOptions options)
        {
            options.Validate();

            var runtime = ContentRuntime.Open(options);
            var loader = await runtime.Register(builder.Lifetime, options.Name);
            runtime.Bundles.Add(options.Name, loader);

            builder.Content.Use(loader, runtime);

            using (GameProfiler.Scope("Content: bind catalogues"))
                await CatalogContent.Bind(builder.Lifetime, loader, loader.RootAssets<ContentRoot>());
            builder.RegisterInstance(loader).As<IContentLoader>();
            builder.RegisterInstance(runtime.Bundles).As<IContentBundles>();
        }

        // The root scope, after AddContent: one more bundle (a ContentBundle asset, usually not preloaded: a WebGL player
        // fetches it), loaded whole with its dependencies and held as long as the root, with a loader over its roots. The
        // registration runs in the background: the root and the scopes after it do not wait for its requests;
        // IContentBundles.Wait(lifetime, name) gives the loader once it is loaded. The catalogues know nothing of it: its
        // objects are loaded from the references of its roots (IContentLoader.RootAssets).
        // OnFirstWait: nothing is fetched until the first Wait of the name (a bundle needed only well after the start).
        public static void AddContentBundle(this IScopeBuilder builder, string name, ContentRegistration registration = ContentRegistration.Background)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("[Content] AddContentBundle without the name of the bundle", nameof(name));

            var runtime = builder.Content.Runtime;
            var lifetime = builder.Lifetime;

            if (registration == ContentRegistration.OnFirstWait)
            {
                runtime.Bundles.Defer(name, source => RegisterInBackground(lifetime, runtime, name, source).NoAwait());
                return;
            }

            RegisterInBackground(lifetime, runtime, name, runtime.Bundles.Begin(name)).NoAwait();
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

        // The top of a task nobody awaits: a failure goes to the log and to every Wait of the bundle.
        private static async UniTask RegisterInBackground(IReadOnlyLifetime lifetime, ContentRuntime runtime, string name,
            UniTaskCompletionSource<IContentLoader> registration)
        {
            try
            {
                registration.TrySetResult(await runtime.Register(lifetime, name));
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated)
            {
                registration.TrySetCanceled();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                registration.TrySetException(exception);
            }
        }
    }
}
