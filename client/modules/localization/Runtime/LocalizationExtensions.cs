using Internal;

namespace Global.Localization
{
    public static class LocalizationExtensions
    {
        // The options asset belongs to the game (GlobalAssets.LocalizationOptions).
        public static IScopeBuilder AddLocalization(this IScopeBuilder builder, LocalizationOptions options)
        {
            builder.RegisterInstance(options);

            builder.Register<LocalizationLoader>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
