using System;
using System.Collections.Generic;
using System.Reflection;

namespace Internal
{
    // What the generated classes and the scope loaders call: the emitted code names this class, the work is in the
    // registry, the ids, the creator and the helpers.
    public static class GeneratedScopes
    {
        // constructed - the implementation types of every Register the class has a slot for; instances - the
        // declared type of every RegisterInstance / RegisterComponent slot (repeated per slot). The builder of
        // the scope is checked against them before the class is built.
        public static void Register(
            string rootId,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed,
            Type[] instances)
        {
            GeneratedScopeRegistry.Register(rootId, factory, constructed, instances);
        }

        public static bool IsRegistered(string rootId)
        {
            return GeneratedScopeRegistry.IsRegistered(rootId);
        }

        public static bool Unregister(string rootId)
        {
            return GeneratedScopeRegistry.Unregister(rootId);
        }

        public static void RegisterVariant(
            string rootId,
            Type viewType,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed,
            Type[] instances)
        {
            GeneratedScopeRegistry.RegisterVariant(rootId, viewType, factory, constructed, instances);
        }

        public static bool HasVariants(string rootId)
        {
            return GeneratedScopeRegistry.HasVariants(rootId);
        }

        public static string VariantKey(string rootId, Type viewType)
        {
            return GeneratedScopeIds.VariantKey(rootId, viewType);
        }

        public static string RootId(MethodInfo method)
        {
            return GeneratedScopeIds.RootId(method);
        }

        internal static IContainer Create(string rootId, ContainerBuilder builder)
        {
            return GeneratedScopeCreator.Create(rootId, builder);
        }

        public static void ThrowDisposeErrors(List<Exception> errors)
        {
            GeneratedScopeDisposeErrors.Throw(errors);
        }

        public static T RequireAlternative<T>(T value, string type) where T : class
        {
            return GeneratedScopeAlternatives.Require(value, type);
        }

        public static T[] WithoutAbsent<T>(T[] values) where T : class
        {
            return GeneratedScopeAlternatives.WithoutAbsent(values);
        }
    }
}
