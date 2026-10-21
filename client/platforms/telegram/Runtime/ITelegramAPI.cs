namespace Global.Publisher.Telegram
{
    // window.telegramShell of the Telegram WebGL template (TemplateData/telegram-shell.js) through
    // TelegramProvider.jslib; TelegramDebugAPI in the editor.
    public interface ITelegramAPI
    {
        // Telegram.WebApp.initData; empty only in a ?tgDebug=1 run outside Telegram without ?tgInitData=.
        string GetInitData();

        // initDataUnsafe.user.language_code ("ru", "en", ...), empty when Telegram does not send it.
        string GetLanguageCode();

        void SetBackButtonVisible(bool isVisible);

        // Presses of the Telegram BackButton since the page loaded.
        int GetBackButtonPresses();
    }
}
