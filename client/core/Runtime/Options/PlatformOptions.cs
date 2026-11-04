using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class PlatformOptions
    {
        [SerializeField] private PlatformType _platform = PlatformType.Website;

        private static Func<bool> _mobileCheck;

        // A build gets Telegram, Steam or WebStress from the define of its profile (web_telegram, steam_*, web_stress), not from this shared
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
#elif PLATFORM_STRESS
                return PlatformType.WebStress;
#else
                if (_platform == PlatformType.Telegram && IsEditor == false)
                    throw new InvalidOperationException(
                        "[Platform] PlatformType.Telegram without the PLATFORM_TELEGRAM define: build with the web_telegram profile");

                if (_platform == PlatformType.Steam && IsEditor == false)
                    throw new InvalidOperationException(
                        "[Platform] PlatformType.Steam without the PLATFORM_STEAM define: build with a steam_* profile");

                if (_platform == PlatformType.WebStress && IsEditor == false)
                    throw new InvalidOperationException(
                        "[Platform] PlatformType.WebStress without the PLATFORM_STRESS define: build with the web_stress profile");

                return _platform;
#endif
            }
        }

        // Unity's own check (it misses iPadOS, which reports itself as a Mac); a platform module with a better check of
        // its own calls SetMobileCheck.
        public bool IsMobile => _mobileCheck?.Invoke() ?? Application.isMobilePlatform;

        public static void SetMobileCheck(Func<bool> check)
        {
            _mobileCheck = check;
        }

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