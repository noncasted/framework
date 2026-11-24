using Internal;

namespace Global.Publisher.Steam
{
    public static class SteamExtensions
    {
        // The Steam API, the inventory, the invites and the achievements on PlatformType.Steam, the NoPlatform* variants
        // elsewhere. The user of Steam (SteamPlatformAuth) is the alternative of IPlatformAuth chosen by the game in its
        // publisher installer.
        public static IScopeBuilder AddSteam(this IScopeBuilder builder, SteamOptions options, PlatformOptions platform)
        {
            builder.RegisterInstance(options);

            if (platform.PlatformType == PlatformType.Steam)
            {
                SteamApi.Start(builder.Lifetime, options, platform);

                builder.Register<SteamClient>()
                       .As<IScopeSetup>();

                builder.Register<SteamPlatformInventory>()
                       .As<IPlatformInventory>()
                       .As<IScopeSetup>();

                builder.Register<SteamInvites>()
                       .As<IPlatformInvites>()
                       .As<IScopeSetup>();

                builder.Register<SteamPlatformAchievements>()
                       .As<IPlatformAchievements>()
                       .As<IScopeSetup>();
            }
            else
            {
                builder.Register<NoPlatformInventory>()
                       .As<IPlatformInventory>();

                builder.Register<NoPlatformInvites>()
                       .As<IPlatformInvites>();

                builder.Register<NoPlatformAchievements>()
                       .As<IPlatformAchievements>();
            }

            return builder;
        }
    }
}
