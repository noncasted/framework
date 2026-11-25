using System;

namespace Internal.Tests
{
    public sealed class ScopeLoaderThrowingSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("setup failed");
        }
    }
}
