using System;

namespace Internal
{
    public static class ScopeContainer
    {
        // The scope container is always the generated class. No class means an exception, there is no fallback.
        public static IContainer Create(string rootId, ContainerBuilder builder)
        {
            ContainerThread.Assert();

            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));

            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return GeneratedScopes.Create(rootId, builder);
        }

        // Entity: a root loaded with several view types has a class per view type, otherwise one class for the root.
        // A view type without its variant class means the asset map is stale: the root is never taken instead.
        public static IContainer CreateEntity(string rootId, Type viewType, ContainerBuilder builder)
        {
            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            if (GeneratedScopes.HasVariants(rootId) == false)
                return Create(rootId, builder);

            var variant = GeneratedScopes.VariantKey(rootId, viewType);

            if (GeneratedScopes.IsRegistered(variant) == false)
            {
                throw new InvalidOperationException(
                    $"Entity root '{rootId}' has no variant for view {viewType.FullName}. The generator splits the " +
                    "root by the view types of its load sites: regenerate the asset map of the project.");
            }

            return Create(variant, builder);
        }
    }
}