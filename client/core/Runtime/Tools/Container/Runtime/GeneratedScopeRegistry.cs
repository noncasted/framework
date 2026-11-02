using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    // The factories of the generated classes by root id.
    [NoAutoStaticsCleanup]
    internal static class GeneratedScopeRegistry
    {
        private static readonly Dictionary<string, GeneratedScopeFactory> _factories =
            new Dictionary<string, GeneratedScopeFactory>(StringComparer.Ordinal);

        // Roots split into entity variants by view type: such a root has no class of its own.
        private static readonly HashSet<string> _variantRoots = new HashSet<string>(StringComparer.Ordinal);

        public static void Register(
            string rootId,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed,
            Type[] instances)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (constructed == null)
                throw new ArgumentNullException(nameof(constructed));

            if (instances == null)
                throw new ArgumentNullException(nameof(instances));

            if (_factories.TryGetValue(rootId, out var existing) == true)
            {
                // Without domain reload, statics survive entering Play mode and RegisterGenerated
                // is called again: the same factory method is not a duplicate but a repeated registration.
                if (existing.Create.Method == factory.Method)
                    return;

                throw new InvalidOperationException(
                    "Generated scope '" + rootId + "' is already registered.");
            }

            _factories.Add(rootId, new GeneratedScopeFactory(factory, constructed, instances));
        }

        public static bool IsRegistered(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                return false;

            return _factories.ContainsKey(rootId);
        }

        public static bool Unregister(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                return false;

            return _factories.Remove(rootId);
        }

        public static void RegisterVariant(
            string rootId,
            Type viewType,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed,
            Type[] instances)
        {
            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            Register(GeneratedScopeIds.VariantKey(rootId, viewType), factory, constructed, instances);
            _variantRoots.Add(rootId);
        }

        // The root is built per view type: a view type without its variant class is an error, not the base root.
        public static bool HasVariants(string rootId)
        {
            return _variantRoots.Contains(rootId);
        }

        public static bool TryGet(string rootId, out GeneratedScopeFactory factory)
        {
            return _factories.TryGetValue(rootId, out factory);
        }
    }
}
