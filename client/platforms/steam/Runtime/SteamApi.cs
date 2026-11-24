using System;
using Internal;
using UnityEngine;
#if UNITY_STANDALONE
using Steamworks;
#endif

namespace Global.Publisher.Steam
{
    // The Steam API of the process: started by AddSteam while the scope is built, so before every Steam service of the
    // scope and of its children, and shut down with the scope. Steamworks.NET exists only in standalone builds and the
    // editor.
    public static class SteamApi
    {
        public static void Start(IReadOnlyLifetime lifetime, SteamOptions options, PlatformOptions platform)
        {
#if UNITY_STANDALONE
            // A build started outside the Steam client (the exe by hand) is started again by Steam, this one quits.
            if (platform.IsEditor == false && SteamAPI.RestartAppIfNecessary(new AppId_t(options.AppId)) == true)
            {
                Application.Quit();
                throw new InvalidOperationException("[Steam] Started outside Steam: Steam starts the game again");
            }

            var result = SteamAPI.InitEx(out var error);

            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException($"[Steam] Steam API is not started: {result} {error}");

            Debug.Log($"[Steam] Started, user {SteamUser.GetSteamID()}");

            lifetime.Listen(SteamAPI.Shutdown);
#else
            throw new PlatformNotSupportedException("[Steam] Steamworks is only in standalone builds");
#endif
        }
    }
}
