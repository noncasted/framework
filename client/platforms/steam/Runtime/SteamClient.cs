using System;
using Internal;
using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // The Steam API of the process: started in the base setup of the global scope on PlatformType.Steam (before every
    // Steam service of the scope and of its children), its callbacks are run every frame, shut down with the scope.
    // Registered on every platform (the constructor does nothing): the Steam services depend on it, and the container
    // generator builds every registration it sees. Steamworks.NET exists only in standalone builds and the editor.
    public interface ISteamClient
    {
        // True after the start on PlatformType.Steam: the Steam services check it before any call.
        bool IsStarted { get; }
    }

    public class SteamClient : ISteamClient, IScopeBaseSetup, IUpdatable
    {
        public SteamClient(PlatformOptions platformOptions, SteamOptions options, IUpdater updater)
        {
            _platformOptions = platformOptions;
            _options = options;
            _updater = updater;
        }

        private readonly PlatformOptions _platformOptions;
        private readonly SteamOptions _options;
        private readonly IUpdater _updater;

        public bool IsStarted { get; private set; }

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            if (_platformOptions.PlatformType != PlatformType.Steam)
                return;

#if UNITY_STANDALONE
            // A build started outside the Steam client (the exe by hand) is started again by Steam, this one quits.
            if (_platformOptions.IsEditor == false && SteamAPI.RestartAppIfNecessary(new AppId_t(_options.AppId)) == true)
            {
                Application.Quit();
                throw new InvalidOperationException("[Steam] Started outside Steam: Steam starts the game again");
            }

            var result = SteamAPI.InitEx(out var error);

            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException($"[Steam] Steam API is not started: {result} {error}");

            IsStarted = true;
            Debug.Log($"[Steam] Started, user {SteamUser.GetSteamID()}");

            _updater.Add(lifetime, this);

            lifetime.Listen(() => {
                IsStarted = false;
                SteamAPI.Shutdown();
            });
#else
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }

        public void OnUpdate(float delta)
        {
#if UNITY_STANDALONE
            SteamAPI.RunCallbacks();
#endif
        }
    }
}
