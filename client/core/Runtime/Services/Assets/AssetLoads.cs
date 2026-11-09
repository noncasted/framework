using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The loads of content after the start as a tool sees them (the client telemetry): every catalogue group
    // (AssetGroup) and every scene of a scope (ServiceScopeSceneLoader) goes through here. A listener that throws does not
    // break a load: the exception goes to ReactiveExceptions.
    public static class AssetLoads
    {
        private static readonly List<AssetLoad> _running = new();
        private static readonly EventSource<AssetLoad> _started = new();
        private static readonly EventSource<AssetLoad> _ended = new();

        // The loads going on now, oldest first.
        public static IReadOnlyList<AssetLoad> Running => _running;

        public static IEventSource<AssetLoad> Started => _started;

        // Finished or failed: see AssetLoad.Error.
        public static IEventSource<AssetLoad> Ended => _ended;

        public static AssetLoad Begin(string key)
        {
            var load = new AssetLoad(key);

            _running.Add(load);
            _started.Invoke(load);

            return load;
        }

        // Without a domain reload a load of the previous run would stay here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRunning()
        {
            _running.Clear();
        }

        internal static void OnEnded(AssetLoad load)
        {
            _running.Remove(load);
            _ended.Invoke(load);
        }
    }
}
