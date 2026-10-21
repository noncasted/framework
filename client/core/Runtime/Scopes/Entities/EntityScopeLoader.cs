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
            var builder = CreateBuilder(parentLifetime, parent, view);

            await construct.Invoke(builder);
            await builder.Events.InvokeBeforeBuild();

            view.CreateViews(builder);

            return await CreateContainer(builder, view, construct.Method);
        }

        public async UniTask<IEntityScopeResult> Load(
            IReadOnlyLifetime parentLifetime,
            IContainer parent,
            IScopeEntityView view,
            Action<IEntityBuilder> construct)
        {
            var builder = CreateBuilder(parentLifetime, parent, view);

            construct.Invoke(builder);
            await builder.Events.InvokeBeforeBuild();

            view.CreateViews(builder);

            return await CreateContainer(builder, view, construct.Method);
        }

        private EntityBuilder CreateBuilder(IReadOnlyLifetime parentLifetime, IContainer parent, IScopeEntityView view)
        {
            var lifetime = parentLifetime.Child();
            var containerBuilder = new ContainerBuilder(view.GetType().Name, parent, lifetime);

            return new EntityBuilder(containerBuilder, view, lifetime, new EventLoop());
        }

        // The scope class is selected by the root and the concrete view type: a variant has its own type.
        private async UniTask<IEntityScopeResult> CreateContainer(
            EntityBuilder builder,
            IScopeEntityView view,
            MethodInfo root)
        {
            builder.RegisterInstance(builder.Events);

            var container = ScopeContainer.CreateEntity(
                GeneratedScopes.RootId(root),
                view.GetType(),
                builder.ContainerBuilder);

            builder.ScopeLifetime.Listen(container.Dispose);

            // Entity roots terminate their own ILifetime, and this is a child lifetime of the container. Component
            // setup is subscribed to the scope lifetime, and without this back link entity updates
            // outlived its view until the end of the session.
            container.Lifetime.Listen(builder.ScopeLifetime.Terminate);

            view.Bind(container);

            builder.Events.Bind(container);
            await builder.Events.RunConstruct(builder.ScopeLifetime);

            return new EntityScopeResult(container, builder.ScopeLifetime);
        }
    }
}