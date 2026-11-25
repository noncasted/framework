using System;

namespace Internal.Tests
{
    internal readonly struct BenchmarkRegistration
    {
        public readonly Type Implementation;
        public readonly ServiceLifetime Lifetime;
        public readonly BenchmarkScopeLevel Scope;
        public readonly Type[] Markers;

        public BenchmarkRegistration(
            Type implementation,
            ServiceLifetime lifetime,
            BenchmarkScopeLevel scope,
            params Type[] markers)
        {
            Implementation = implementation;
            Lifetime = lifetime;
            Scope = scope;
            Markers = markers ?? Array.Empty<Type>();
        }
    }
}
