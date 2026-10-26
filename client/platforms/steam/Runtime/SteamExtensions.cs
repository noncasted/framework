using Internal;

namespace Global.Publisher.Steam
{
    public static class SteamExtensions
    {
        // The Steam API, the inventory, the invites and the achievements. Registered on every platform, not as alternatives: they are set
        // up only on PlatformType.Steam (ISteamClient.IsStarted, IsSupported). The user of Steam (SteamPlatformAuth)
        // is the alternative of IPlatformAuth chosen by the game in its publisher installer.
        public static IScopeBuilder AddSteam(this IScopeBuilder builder, SteamOptions options)
        {
            builder.RegisterInstance(options);

            builder.Register<SteamClient>()
                   .As<ISteamClient>()
                   .As<IScopeBaseSetup>();

            builder.Register<SteamPlatformInventory>()
                   .As<IPlatformInventory>()
                   .As<IScopeSetup>();

            builder.Register<SteamInvites>()
                   .As<IPlatformInvites>()
                   .As<IScopeSetup>();

            builder.Register<SteamPlatformAchievements>()
                   .As<IPlatformAchievements>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
