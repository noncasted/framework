using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Publisher
{
    // Items the user owns in the platform (the Steam Inventory: item definitions of the Steamworks item schema).
    // Steam only so far (Steam.SteamPlatformInventory, registered on every platform).
    public interface IPlatformInventory
    {
        // False where the platform has no inventory (not Steam): nothing else may be called then.
        bool IsSupported { get; }

        // The definitions of the owned items, each once. Asks the platform every time (a drop or a grant of the
        // site may have come since the last call).
        UniTask<IReadOnlyList<int>> GetOwnedItems(IReadOnlyLifetime lifetime);

        // Playtime drops: asks the platform for one drop of each generator; the platform decides whether the time of
        // a drop has come (the Steam playtime generators of the item schema).
        void TriggerDrops(IReadOnlyList<int> generators);
    }
}
