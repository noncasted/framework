using Global.Audio;
using Internal;

namespace Global.Settings
{
    public static class SettingsExtensions
    {
        // A game without settings of its own: the volumes and the language, consumed as ISettings.
        public static IScopeBuilder AddSettings(this IScopeBuilder builder, SettingsOptions options)
        {
            builder.RegisterInstance<SettingsOptions<SettingsSave>>(options);

            builder.Register<Settings>()
                   .As<ISettings>()
                   .As<ISettings<SettingsSave>>()
                   .As<IAudioVolume>()
                   .As<IScopeSetupAsync>();

            return builder;
        }

        // A game with settings of its own (see Settings<TSave>): TService is the interface of the game over
        // ISettings<TSave>, what its views and services inject.
        public static IScopeBuilder AddSettings<TSettings, TSave, TService>(
            this IScopeBuilder builder,
            SettingsOptions<TSave> options)
            where TSettings : Settings<TSave>, TService
            where TSave : SettingsSave, new()
            where TService : class
        {
            builder.RegisterInstance<SettingsOptions<TSave>>(options);

            builder.Register<TSettings>()
                   .As<ISettings>()
                   .As<ISettings<TSave>>()
                   .As<TService>()
                   .As<IAudioVolume>()
                   .As<IScopeSetupAsync>();

            return builder;
        }
    }
}
