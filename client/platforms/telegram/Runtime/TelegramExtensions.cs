using Internal;

namespace Global.Publisher.Telegram
{
    public static class TelegramExtensions
    {
        // The bridge to window.telegramShell (TemplateData/telegram-shell.js of the Telegram WebGL template), the debug
        // one in the editor. Built on every platform (the constructors do nothing), only Telegram calls it. The user of
        // Telegram (TelegramPlatformAuth) and its BackButton (TelegramBackButton) are alternatives of IPlatformAuth and
        // IPlatformBackButton chosen by the game in its publisher installer.
        public static IScopeBuilder AddTelegramAPI(this IScopeBuilder builder, PlatformOptions platformOptions)
        {
            if (platformOptions.IsEditor == true)
            {
                builder.Register<TelegramDebugAPI>()
                       .As<ITelegramAPI>();
            }
            else
            {
                builder.Register<TelegramExternAPI>()
                       .As<ITelegramAPI>();
            }

            return builder;
        }
    }
}
