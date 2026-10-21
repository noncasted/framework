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

        // Entity: the variant class is selected by the concrete view type.
        public static IContainer CreateEntity(string rootId, Type viewType, ContainerBuilder builder)
        {
            if (viewType != null)
            {
                var variant = GeneratedScopes.VariantKey(rootId, viewType);

                if (GeneratedScopes.IsRegistered(variant) == true)
                    return Create(variant, builder);
            }

            return Create(rootId, builder);
        }
    }
}