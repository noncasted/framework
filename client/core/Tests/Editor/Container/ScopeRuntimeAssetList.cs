using System.Collections.Generic;

namespace Internal.Tests
{
    public sealed class ScopeRuntimeAssetList
    {
        public ScopeRuntimeAssetList(IReadOnlyList<ScopeRuntimeAsset> assets)
        {
            Assets = assets;
        }

        public IReadOnlyList<ScopeRuntimeAsset> Assets { get; }
    }
}
