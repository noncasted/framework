using System;

using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IServiceScopeLoader
    {
        UniTask<ILoadedScope> Load(ScopeLoadOptions options);
    }

    public class ServiceScopeLoader : IServiceScopeLoader
    {
        public async UniTask<ILoadedScope> Load(ScopeLoadOptions options)
        {
            var sceneName = options.SceneLoader.Name;

            // A child is disposed together with its parent: the parent has to be a scope of this loader.
            var parent = options.Parent == null ? null : options.Parent as LoadedScope;

            if (options.Parent != null && parent == null)
            {
                throw new InvalidOperationException(
                    $"Scope '{sceneName}': the parent {options.Parent.GetType().FullName} is not a scope of ServiceScopeLoader.");
            }

            // The stages are the same for all scopes, so the measurement lives here rather than in every
            // extension: in the trace they go under the stage that the scope itself opened.
            using var stage = GameProfiler.Scope($"Scope: {sceneName}");

            // The lifetime comes first: the scenes of the content bundles are loaded on it.
            var lifetime = options.Parent == null ? new Lifetime() : options.Parent.Lifetime.Child();
            var content = new ScopeContent(options.Parent);
            var sceneLoader = new ServiceScopeSceneLoader(lifetime, content);
            ScopeBuilder builder = null;
            IContainer container = null;

            try
            {
                IServiceScopeBinder binder;

                using (GameProfiler.Scope("Registry scene"))
                    binder = await options.SceneLoader.Load(sceneLoader);

                builder = CreateBuilder(binder);

                using (GameProfiler.Scope("Construct"))
                    await options.ConstructCallback.Invoke(builder);

                using (GameProfiler.Scope("Assets"))
                    await builder.Events.InvokeBeforeBuild();

                using (GameProfiler.Scope("Container"))
                {
                    builder.RegisterInstance(builder.Events);
                    container = ScopeContainer.Create(options.RootId, builder.ContainerBuilder);
                }

                builder.Events.Bind(container);

                using (GameProfiler.Scope("Setup"))
                    await builder.Events.RunConstruct(builder.ScopeLifetime);
            }
            catch (Exception exception)
            {
                // A scope that failed to load is torn down as far as it got: otherwise its scenes stay loaded (a retry
                // fails on a duplicate scene), its asset groups are never released and its lifetime outlives it.
                await TearDownFailed(exception, lifetime, builder, container, sceneLoader);
                throw;
            }

            var loadResult = new LoadedScope(
                container,
                builder.ScopeLifetime,
                builder.Events,
                sceneLoader.Results);

            if (parent == null)
                return loadResult;

            try
            {
                parent.AddChild(loadResult);
            }
            catch
            {
                // The parent went down while this scope was loading: nobody would dispose it.
                await loadResult.Dispose();
                throw;
            }

            return loadResult;

            // Without a parent the root is built: its container does not depend on anyone.
            ScopeBuilder CreateBuilder(IServiceScopeBinder binder)
            {
                if (options.Parent == null)
                {
                    return new ScopeBuilder(
                        new ContainerBuilder(sceneName, lifetime),
                        sceneLoader,
                        content,
                        binder,
                        lifetime,
                        null,
                        new EventLoop(),
                        options.IsMock);
                }

                var containerBuilder = new ContainerBuilder(sceneName, options.Parent.Container, lifetime);

                return new ScopeBuilder(
                    containerBuilder,
                    sceneLoader,
                    content,
                    binder,
                    lifetime,
                    options.Parent,
                    new EventLoop(),
                    options.IsMock);
            }
        }

        // The setup did not complete, so the dispose listeners are not called: only what the load acquired is
        // released. An error of the teardown itself is not lost: it goes out together with the load error.
        private static async UniTask TearDownFailed(
            Exception failure,
            ILifetime lifetime,
            ScopeBuilder builder,
            IContainer container,
            ServiceScopeSceneLoader sceneLoader)
        {
            var teardown = new ScopeTeardown();

            teardown.Step(lifetime.Terminate);

            if (container != null)
                teardown.Step(container.Dispose);

            await teardown.StepAwaited(() => sceneLoader.Results.InvokeAwaited(scene => scene.Unload()));

            if (builder != null)
                await teardown.StepAwaited(builder.Events.InvokeAfterDispose);

            teardown.ThrowWithLoadFailure(failure);
        }
    }
}
