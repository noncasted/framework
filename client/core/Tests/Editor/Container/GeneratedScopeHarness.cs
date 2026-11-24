using System;

namespace Internal.Tests
{
    // Builds a generated scope the way the loaders do: the installer writes into ContainerBuilder,
    // ScopeContainer.Create builds the generated class.
    internal static class GeneratedScopeHarness
    {
        public static IContainer Build(Action<IBuilder> root, IContainer parent = null)
        {
            var rootId = GeneratedRoots.Register(root.Method);
            var lifetime = parent != null ? parent.Lifetime.Child() : new Lifetime();
            var containerBuilder = parent != null
                ? new ContainerBuilder(rootId, parent, lifetime)
                : new ContainerBuilder(rootId, lifetime);
            var builder = new RootBuilder(containerBuilder, new EventLoop(), lifetime);

            root.Invoke(builder);
            builder.RegisterInstance(builder.Events);

            var container = ScopeContainer.Create(rootId, containerBuilder);
            builder.Events.Bind(container);
            return container;
        }
    }
}
