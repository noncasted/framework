using Internal;
#if UNITY_STANDALONE
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // Runs the callbacks of the Steam API (SteamApi) every frame. Registered only on PlatformType.Steam.
    public class SteamClient : IScopeSetup, IUpdatable
    {
        public SteamClient(IUpdater updater)
        {
            _updater = updater;
        }

        private readonly IUpdater _updater;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
#if UNITY_STANDALONE
            SteamAPI.RunCallbacks();
#endif
        }
    }
}
