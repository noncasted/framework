using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Internal;
#if UNITY_STANDALONE
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // The achievements of the Steamworks app. Steam loads the stats and the achievements of the user by itself after
    // the start of the API (UserStatsReceived_t): an unlock that comes before them waits and is set when they arrive.
    // Every set achievement is stored at once (StoreStats shows the notification of the overlay).
    public class SteamPlatformAchievements : IPlatformAchievements, IScopeSetup
    {
        public SteamPlatformAchievements(ILog<SteamPlatformAchievements> log)
        {
            _log = log;
        }

        private readonly ILog _log;
        private readonly List<string> _pending = new();

        public bool IsSupported => true;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
#if UNITY_STANDALONE
            var callback = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
            lifetime.Listen(callback.Dispose);
#endif
        }

        public void Unlock(string name)
        {
#if UNITY_STANDALONE
            if (IsLoaded() == false)
            {
                if (_pending.Contains(name) == false)
                    _pending.Add(name);

                return;
            }

            // Already unlocked (the same event again, an earlier run): nothing to store.
            if (Set(name) == false)
                return;

            if (SteamUserStats.StoreStats() == false)
                throw new InvalidOperationException($"[Steam] The stats are not stored after achievement {name}");
#else
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }

#if UNITY_STANDALONE
        // The schema of the app comes together with the stats of the user: no achievements until then.
        private static bool IsLoaded()
        {
            return SteamUserStats.GetNumAchievements() > 0;
        }

        // True when the achievement is set now, false when it was unlocked before.
        private bool Set(string name)
        {
            if (SteamUserStats.GetAchievement(name, out var isAchieved) == false)
                throw new ArgumentException($"[Steam] No achievement {name} in the app", nameof(name));

            if (isAchieved == true)
                return false;

            if (SteamUserStats.SetAchievement(name) == false)
                throw new InvalidOperationException($"[Steam] Achievement {name} is not set");

            _log.Info("Achievement {Name} is unlocked", name);
            return true;
        }

        private void OnStatsReceived(UserStatsReceived_t received)
        {
            // The stats of other users (none are requested) and of other apps are not ours.
            if (received.m_steamIDUser != SteamUser.GetSteamID())
                return;

            if (received.m_eResult != EResult.k_EResultOK)
                throw new InvalidOperationException($"[Steam] The stats of the user are not received: {received.m_eResult}");

            if (_pending.Count == 0)
                return;

            // Every waiting name is set apart: an unknown one does not stop the others, the set ones are stored and the
            // queue is emptied whatever failed; the first failure is thrown after that.
            var isChanged = false;
            ExceptionDispatchInfo failure = null;

            foreach (var name in _pending)
            {
                try
                {
                    isChanged |= Set(name);
                }
                catch (Exception exception)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }

            _pending.Clear();

            if (isChanged == true && SteamUserStats.StoreStats() == false)
                throw new InvalidOperationException("[Steam] The stats are not stored after the waiting achievements");

            failure?.Throw();
        }
#endif
    }
}
