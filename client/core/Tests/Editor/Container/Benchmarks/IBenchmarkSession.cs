using System;

namespace Internal.Tests
{
    internal interface IBenchmarkSession : IDisposable
    {
        int ResolveAllPhases();
        object ResolveSingleton();
        object ResolveTransient();
        object Resolve(BenchmarkScopeLevel level, Type type);
    }
}
