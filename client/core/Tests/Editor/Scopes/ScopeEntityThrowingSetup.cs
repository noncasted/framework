using System;

namespace Internal.Tests
{
    public sealed class ScopeEntityThrowingSetup : IScopeSetup
    {
        public ScopeEntityThrowingSetup(IContainer container)
        {
            ScopeLoaderProbe.SetContainer(container);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("entity setup failed");
        }
    }
}
