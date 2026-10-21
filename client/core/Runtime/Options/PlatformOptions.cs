using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class PlatformOptions
    {
        [SerializeField] private PlatformType _platform = PlatformType.Website;

        // A build gets Telegram or Steam from the define of its profile (web_telegram, steam_*), not from this shared
        // asset: the builds would overwrite one field. The serialized Telegram or Steam is for the editor only (a
        // debug run of the platform: the debug API of Telegram, a running Steam client for Steam).
        public PlatformType PlatformType
        {
            get
            {
#if PLATFORM_TELEGRAM
                return PlatformType.Telegram;
#elif PLATFORM_STEAM
                return PlatformType.Steam;
#else
                if (_platform == PlatformType.Telegram && IsEditor == false)
                    throw new InvalidOperationException(
                        "[Platform] PlatformType.Telegram without the PLATFORM_TELEGRAM define: build with the web_telegram profile");

                if (_platform == PlatformType.Steam && IsEditor == false)
                    throw new InvalidOperationException(
                        "[Platform] PlatformType.Steam without the PLATFORM_STEAM define: build with a steam_* profile");

                return _platform;
#endif
            }
            set => _platform = value;
        }

        // Unity's own check (it misses iPadOS, which reports itself as a Mac); a platform module with a better check of
        // its own sets MobileCheck.
        public bool IsMobile => MobileCheck?.Invoke() ?? Application.isMobilePlatform;

        public static Func<bool> MobileCheck { get; set; }

        public bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
    }
}