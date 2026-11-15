using System;
using System.Reflection;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IEntityScopeLoader
    {
        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct);

        UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct);
    }

    public class EntityScopeLoader : IEntityScopeLoader
    {
        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Func<IEntityBuilder, UniTask> construct)
        {
            using var stage = OpenSpan(view);

            var builder = CreateBuilder(parentLifetime, parent, view, out var teardown);

            try
            {
                await construct.Invoke(builder);

                // The lifetime ended before or during construct: the build events would retain the assets of an entity
                // that is already torn down.
                builder.ScopeLifetime.Token.ThrowIfCancellationRequested();
                await builder.Events.InvokeBeforeBuild();

                view.CreateViews(builder);

                return await CreateContainer(builder, view, construct.Method, teardown);
            }
            catch
            {
                await teardown.Fail(builder.ScopeLifetime);
                throw;
            }
        }

        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct)
        {
            using var stage = OpenSpan(view);

            var builder = CreateBuilder(parentLifetime, parent, view, out var teardown);

            try
            {
                construct.Invoke(builder);

                // The lifetime ended before or during construct: the build events would retain the assets of an entity
                // that is already torn down.
                builder.ScopeLifetime.Token.ThrowIfCancellationRequested();
                await builder.Events.InvokeBeforeBuild();

                view.CreateViews(builder);

                return await CreateContainer(builder, view, construct.Method, teardown);
            }
            catch
            {
                await teardown.Fail(builder.ScopeLifetime);
                throw;
            }
        }

        // The phases of an entity go under its span, not flat into the stage. The name is built only under the profiler:
        // it is a string per entity.
        private static IProfilerScope OpenSpan(IScopeEntityView view)
        {
            return GameProfiler.IsRunning == true
                ? GameProfiler.Scope($"Entity: {view.GetType().Name}")
                : NullProfilerScope.Instance;
        }

        // The end of the scope lifetime tears the entity down: the dispose phase, the container, then the asset
        // groups. A load that fails ends the lifetime and is torn down the same way, without the dispose phase unless
        // the setup has completed.
        private EntityBuilder CreateBuilder(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            out EntityTeardown teardown)
        {
            var lifetime = parentLifetime.Child();
            var containerBuilder = new ContainerBuilder(view.GetType().Name, parent, lifetime);
            var builder = new EntityBuilder(containerBuilder, view, lifetime, new EventLoop());

            teardown = new EntityTeardown(builder.Events);
            lifetime.Listen(teardown.Run);

            return builder;
        }

        // The scope class is selected by the root and the concrete view type: a variant has its own type.
        private async UniTask<IEntityScopeResult> CreateContainer(
            EntityBuilder builder,
            IScopeEntityView view,
            MethodInfo root,
            EntityTeardown teardown)
        {
            // The lifetime ended while construct or the build events waited: its teardown has run already, and nothing
            // would dispose a container built now.
            builder.ScopeLifetime.Token.ThrowIfCancellationRequested();

            builder.RegisterInstance(builder.Events);

            var container = ScopeContainer.CreateEntity(
                GeneratedScopes.RootId(root),
                view.GetType(),
                builder.ContainerBuilder);

            teardown.SetContainer(container);

            // An entity root without WithParameter(ScopeLifetime) terminates the lifetime of the container. Component
            // setup is subscribed to the scope lifetime, and without this back link entity updates
            // outlived its view until the end of the session.
            container.Lifetime.Listen(builder.ScopeLifetime.Terminate);

            view.Bind(container);

            builder.Events.Bind(container);

            // A teardown that comes during the setup waits for it: the services that have started their setup are
            // disposed, and the setup does not resolve from a disposed container.
            teardown.BeginConstruct();

            try
            {
                await builder.Events.RunConstruct(builder.ScopeLifetime);
                teardown.EndConstruct(true);
            }
            catch
            {
                teardown.EndConstruct(false);
                throw;
            }

            builder.ScopeLifetime.Token.ThrowIfCancellationRequested();

            // An entity has no Initialize of its own: it is loaded once its setup is done.
            await builder.Events.RunLoaded(builder.ScopeLifetime);

            return new EntityScopeResult(container, builder.ScopeLifetime);
        }

        private sealed class EntityTeardown
        {
            public EntityTeardown(IEventLoop events)
            {
                _events = events;
            }

            private readonly IEventLoop _events;

            private IContainer _container;

            // The result of the setup: true when it has completed, so the dispose phase runs.
            private UniTaskCompletionSource<bool> _construct;
            private UniTaskCompletionSource _over;

            public void SetContainer(IContainer container)
            {
                _container = container;
            }

            public void BeginConstruct()
            {
                _construct = new UniTaskCompletionSource<bool>();
            }

            public void EndConstruct(bool isCompleted)
            {
                _construct.TrySetResult(isCompleted);
            }

            public void Run()
            {
                _over = new UniTaskCompletionSource();
                Process().NoAwait();
            }

            // A failed load ends the lifetime. When it had ended before, its teardown has released only what was
            // retained by then: the load may have retained more after it (an async construct, the build events).
            public async UniTask Fail(ILifetime lifetime)
            {
                var over = _over;

                lifetime.Terminate();

                if (over == null)
                    return;

                await over.Task;
                await _events.InvokeAfterDispose();
            }

            // A failing step does not stop the teardown; the errors go to the UniTask unobserved exception handler.
            private async UniTaskVoid Process()
            {
                var teardown = new ScopeTeardown();

                if (_construct != null && await _construct.Task == true)
                    await teardown.StepAwaited(_events.RunDispose);

                if (_container != null)
                    teardown.Step(_container.Dispose);

                await teardown.StepAwaited(_events.InvokeAfterDispose);
                _over.TrySetResult();

                teardown.ThrowErrors();
            }
        }
    }
}