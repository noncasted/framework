using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // Common base of catalogue groups: a retain counter and a guard against accessing an unloaded group.
    // Groups live in statics of the generated catalogues and, without a domain reload, survive
    // exiting Play mode, while not all scopes are disposed on exit and Release is not always called.
    // So groups are reset at the start of every run, otherwise a run would take the data of the previous load.
    [NoAutoStaticsCleanup]
    public abstract class AssetGroup
    {
        private static readonly List<AssetGroup> Instances = new();

        private int _retainCount;
        private UniTaskCompletionSource _loading;

        protected AssetGroup()
        {
            Instances.Add(this);
        }

        // Run number: handles of a previous run belong to an already recreated Addressables.
        protected static int Session { get; private set; }

        public bool IsLoaded { get; private set; }

        // Nested loaders of static groups are all named Loader, so the name for logs and the profiler
        // is taken from here rather than from GetType().
        public virtual string Name => GetType().Name;

        // A group outside Addressables lives in Resources and ships in the player data: it needs no bundle,
        // the first access loads it synchronously, so Retain is optional. There is no reason to unload it.
        public virtual bool IsAddressable => true;

        // A retain counts from the call, not from the end of the load: its owner releases it also when the load
        // failed or has not finished yet (a scope disposed while its groups are loading).
        public async UniTask Retain()
        {
            _retainCount++;

            if (IsLoaded)
                return;

            // A group may be retained concurrently (startup preload and the menu scope): the second
            // call awaits the same load instead of acquiring a second handle. Preserve does not fit here:
            // it does not allow awaiting an unfinished task twice, while a completion source does.
            if (_loading == null)
            {
                _loading = new UniTaskCompletionSource();
                Load(_loading).Forget();
            }

            await _loading.Task;
        }

        public void Release()
        {
            if (_retainCount == 0)
                throw new InvalidOperationException($"[{Name}] Release without a Retain");

            _retainCount--;

            if (_retainCount > 0 || IsAddressable == false)
                return;

            // A load in progress is not interrupted: it completes and, with nobody retaining the group, unloads what
            // it loaded (unloading now would release a handle that is still loading, the fields are not set yet).
            // After a failed load there is nothing to unload.
            if (IsLoaded == false)
                return;

            UnloadGroup();
            IsLoaded = false;
            _loading = null;
        }

        public void EnsureLoaded()
        {
            if (IsLoaded)
                return;

            if (IsAddressable)
                throw new InvalidOperationException($"{Name} is not loaded");

            LoadImmediately();
            IsLoaded = true;
        }

        protected abstract UniTask LoadGroup();

        protected abstract void UnloadGroup();

        // Releases what a failed LoadGroup loaded before the failure (a handle, decoded clips).
        protected virtual void UnloadFailedGroup()
        {
            UnloadGroup();
        }

        // Only groups from Resources support synchronous loading.
        protected virtual void LoadImmediately()
        {
            throw new NotSupportedException($"{Name} is addressable and loads only through Retain");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            Session++;

            foreach (var group in Instances)
                group.Reset();
        }

        private void Reset()
        {
            if (IsLoaded)
                UnloadGroup();

            _retainCount = 0;
            _loading = null;
            IsLoaded = false;
        }

        private async UniTask Load(UniTaskCompletionSource loading)
        {
            try
            {
                await LoadGroup();
            }
            catch (Exception exception)
            {
                // The next Retain must retry a failed load on a clean group rather than get the same error.
                if (_loading == loading)
                {
                    _loading = null;
                    UnloadFailedGroup();
                }

                loading.TrySetException(exception);
                return;
            }

            // A load that outlived a reset does not mark the group as loaded.
            if (_loading != loading)
            {
                loading.TrySetResult();
                return;
            }

            // Released to zero while it was loading: what it loaded is unloaded at once.
            if (_retainCount == 0 && IsAddressable == true)
            {
                _loading = null;
                UnloadGroup();
                loading.TrySetResult();
                return;
            }

            IsLoaded = true;
            loading.TrySetResult();
        }
    }
}
