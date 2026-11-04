namespace Internal
{
    public enum PlatformType
    {
        // Web
        Website = 110,
        ItchIO = 120,

        // Telegram Mini App: the web_telegram build profile (define PLATFORM_TELEGRAM).
        Telegram = 130,

        // A web build for stress tests: the web_stress build profile (define PLATFORM_STRESS); a website otherwise.
        WebStress = 140,

        // Desktop: the steam_windows / steam_linux build profiles (define PLATFORM_STEAM).
        Steam = 310,

        // Mobile
        IOS = 210,
        Android = 220,
    }
}