using System.Collections.Generic;

namespace Internal.Tests
{
    public sealed class ScopeRuntimeDepList
    {
        public ScopeRuntimeDepList(IReadOnlyList<IScopeRuntimeDep> deps)
        {
            Deps = deps;
        }

        public IReadOnlyList<IScopeRuntimeDep> Deps { get; }
    }
}
