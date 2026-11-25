using System.Collections.Generic;

namespace Internal.Tests
{
    public sealed class ScopeCodegenRow
    {
        public ScopeCodegenRow(IReadOnlyList<IScopeCodegenTier> tiers)
        {
            Tiers = tiers;
        }

        public IReadOnlyList<IScopeCodegenTier> Tiers { get; }
    }
}
