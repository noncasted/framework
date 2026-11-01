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
            var builder = CreateBuilder(parentLifetime, parent, view, out var teardown);

            try
            {
                await construct.Invoke(builder);
                await builder.Events.InvokeBeforeBuild();

                view.CreateViews(builder);

                return await CreateContainer(builder, view, construct.Method, teardown);
            }
            catch
            {
                builder.ScopeLifetime.Terminate();
                throw;
            }
        }

        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct)
        {
            var builder = CreateBuilder(parentLifetime, parent, view, out var teardown);

            try
            {
                construct.Invoke(builder);
                await builder.Events.InvokeBeforeBuild();

                view.CreateViews(builder);

                return await CreateContainer(builder, view, construct.Method, teardown);
            }
            catch
            {
                builder.ScopeLifetime.Terminate();
                throw;
            }
        }

        // The end of the scope lifetime tears the entity down: the dispose phase, the container, then the asset
        // groups. A load that fails ends the lifetime and is torn down the same way, without the dispose phase.
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

            teardown.Container = container;

            // An entity root without WithParameter(ScopeLifetime) terminates the lifetime of the container. Component
            // setup is subscribed to the scope lifetime, and without this back link entity updates
            // outlived its view until the end of the session.
            container.Lifetime.Listen(builder.ScopeLifetime.Terminate);

            view.Bind(container);

            builder.Events.Bind(container);
            await builder.Events.RunConstruct(builder.ScopeLifetime);
            teardown.IsConstructed = true;

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

            public IContainer Container;
            public bool IsConstructed;

            public void Run()
            {
                Process().NoAwait();
            }

            // A failing step does not stop the teardown; the errors go to the UniTask unobserved exception handler.
            private async UniTaskVoid Process()
            {
                var teardown = new ScopeTeardown();

                if (IsConstructed == true)
                    await teardown.StepAwaited(_events.RunDispose);

                if (Container != null)
                    teardown.Step(Container.Dispose);

                await teardown.StepAwaited(_events.InvokeAfterDispose);

                teardown.ThrowErrors();
            }
        }
    }
}