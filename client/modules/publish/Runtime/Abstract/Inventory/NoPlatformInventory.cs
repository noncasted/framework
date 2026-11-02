using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    public class NoPlatformInventory : IPlatformInventory
    {
        public bool IsSupported => false;

        public UniTask<IReadOnlyList<int>> GetOwnedItems(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("[Publisher] The inventory is read on a platform without one");
        }

        public void TriggerDrops(IReadOnlyList<int> generators)
        {
            throw new InvalidOperationException("[Publisher] Drops are triggered on a platform without an inventory");
        }
    }
}
