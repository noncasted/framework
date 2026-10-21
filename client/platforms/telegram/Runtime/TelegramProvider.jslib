// Bridge to window.telegramShell of the Telegram WebGL template (TemplateData/telegram-shell.js).
// A build of the Telegram platform with another template fails here loudly.
mergeInto(LibraryManager.library,
    {
        TelegramInitData: function () {
            if (window.telegramShell === undefined)
                throw new Error("[Telegram] telegram-shell.js is not loaded: the Telegram platform needs the Telegram template");

            const value = window.telegramShell.initData();
            const bufferSize = lengthBytesUTF8(value) + 1;
            const buffer = _malloc(bufferSize);
            stringToUTF8(value, buffer, bufferSize);

            return buffer;
        },

        TelegramLanguageCode: function () {
            if (window.telegramShell === undefined)
                throw new Error("[Telegram] telegram-shell.js is not loaded: the Telegram platform needs the Telegram template");

            const value = window.telegramShell.languageCode();
            const bufferSize = lengthBytesUTF8(value) + 1;
            const buffer = _malloc(bufferSize);
            stringToUTF8(value, buffer, bufferSize);

            return buffer;
        },

        TelegramSetBackButtonVisible: function (isVisible) {
            if (window.telegramShell === undefined)
                throw new Error("[Telegram] telegram-shell.js is not loaded: the Telegram platform needs the Telegram template");

            window.telegramShell.setBackButtonVisible(isVisible !== 0);
        },

        TelegramBackButtonPresses: function () {
            if (window.telegramShell === undefined)
                throw new Error("[Telegram] telegram-shell.js is not loaded: the Telegram platform needs the Telegram template");

            return window.telegramShell.backButtonPresses();
        }
    });
