using System;
using System.Reflection;

namespace Internal.Tests
{
    // Builds a generated scope the way the loaders do: the installer writes into ContainerBuilder,
    // ScopeContainer.Create builds the generated class. RuntimeInitializeOnLoadMethod is not called in edit mode,
    // so the generated class of the root is registered here.
    internal static class GeneratedScopeHarness
    {
        public static IContainer Build(Action<IBuilder> root, IContainer parent = null)
        {
            var rootId = Register(root.Method);
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

        // Registers the generated class of a root method and returns its root id.
        public static string Register(MethodInfo root)
        {
            var rootId = GeneratedScopes.RootId(root);

            if (GeneratedScopes.IsRegistered(rootId) == true)
                return rootId;

            var name = root.DeclaringType.Name + root.Name + "Container";
            var type = root.DeclaringType.Assembly.GetType(root.DeclaringType.Namespace + "." + name);

            if (type == null)
                throw new InvalidOperationException("No generated class " + name + " for " + rootId);

            var method = type.GetMethod("RegisterGenerated", BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, null);
            return rootId;
        }
    }
}
