using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
#if UNITY_STANDALONE
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // The Steam Inventory of the user. Every request is a result handle that Steam fills asynchronously and reports
    // with SteamInventoryResultReady_t; the handle is destroyed once it is read.
    public class SteamPlatformInventory : IPlatformInventory, IScopeSetup
    {
        public SteamPlatformInventory(ILog<SteamPlatformInventory> log)
        {
            _log = log;
        }

        private readonly ILog _log;

#if UNITY_STANDALONE
        private readonly Dictionary<SteamInventoryResult_t, Action<EResult>> _pending = new();
#endif

        public bool IsSupported => true;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
#if UNITY_STANDALONE
            var callback = Callback<SteamInventoryResultReady_t>.Create(OnResultReady);
            lifetime.Listen(callback.Dispose);
#endif
        }

        public async UniTask<IReadOnlyList<int>> GetOwnedItems(IReadOnlyLifetime lifetime)
        {
#if UNITY_STANDALONE
            if (SteamInventory.GetAllItems(out var handle) == false)
                throw new InvalidOperationException("[Steam] GetAllItems is refused");

            // Read and freed when Steam answers, also when the caller has given up meanwhile.
            var completion = new UniTaskCompletionSource<IReadOnlyList<int>>();

            _pending.Add(handle, result => {
                try
                {
                    completion.TrySetResult(ReadDefinitions(handle, result));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    SteamInventory.DestroyResult(handle);
                }
            });

            return await completion.Task.AttachExternalCancellation(lifetime.Token);
#else
            await UniTask.CompletedTask;
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }

        public void TriggerDrops(IReadOnlyList<int> generators)
        {
#if UNITY_STANDALONE
            foreach (var generator in generators)
            {
                var definition = new SteamItemDef_t(generator);

                if (SteamInventory.TriggerItemDrop(out var handle, definition) == false)
                    throw new InvalidOperationException($"[Steam] TriggerItemDrop of {generator} is refused");

                // The dropped item (if Steam gave one) is read by the next GetOwnedItems: the result is only freed.
                // Not an error of the game: Steam answers k_EResultFail to most of these calls (seen for every generator
                // on a start), so the answer is only logged.
                _pending.Add(handle, result => {
                    if (result != EResult.k_EResultOK)
                        _log.Debug("The drop of the generator {Generator}: {Result}", generator, result);

                    SteamInventory.DestroyResult(handle);
                });
            }
#else
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }

#if UNITY_STANDALONE
        private static IReadOnlyList<int> ReadDefinitions(SteamInventoryResult_t handle, EResult result)
        {
            if (result != EResult.k_EResultOK)
                throw new InvalidOperationException($"[Steam] The inventory is not read: {result}");

            uint count = 0;

            if (SteamInventory.GetResultItems(handle, null, ref count) == false)
                throw new InvalidOperationException("[Steam] The count of the inventory items is not read");

            var details = new SteamItemDetails_t[count];

            if (SteamInventory.GetResultItems(handle, details, ref count) == false)
                throw new InvalidOperationException("[Steam] The inventory items are not read");

            var definitions = new List<int>();

            foreach (var item in details)
            {
                var definition = item.m_iDefinition.m_SteamItemDef;

                if (definitions.Contains(definition) == false)
                    definitions.Add(definition);
            }

            return definitions;
        }

        private void OnResultReady(SteamInventoryResultReady_t ready)
        {
            // Results of other requests (none so far) are not ours.
            if (_pending.Remove(ready.m_handle, out var complete) == false)
                return;

            complete(ready.m_result);
        }
#endif
    }
}
