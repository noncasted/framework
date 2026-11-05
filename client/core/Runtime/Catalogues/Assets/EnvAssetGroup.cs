using System;
using System.Collections.Generic;

namespace Internal
{
    public abstract class EnvAssetGroup : AssetGroup<EnvAssetGroupAsset>
    {
        // Every asset of the loaded group of one type: for lookups that are built from all the assets of a kind (node
        // styles by key) instead of the generated properties.
        public IReadOnlyList<T> GetAll<T>() where T : EnvAsset
        {
            if (Asset == null)
                throw new InvalidOperationException($"{Name} is not loaded: its assets are asked before the group is retained");

            return Asset.GetAll<T>();
        }
    }
}
