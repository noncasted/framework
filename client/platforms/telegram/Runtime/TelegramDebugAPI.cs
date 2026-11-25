using System;
using Internal;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Security.Cryptography;
using System.Text;
#endif

namespace Global.Publisher.Telegram
{
    // The Telegram platform in the editor (PlatformType.Telegram in the options, or a Telegram build profile active):
    // no page around the game, so the init data is signed here as Telegram signs it (the data check string of the Bot
    // API), with TelegramBotToken of the settings file the project names (TelegramEditorOptions) - the token the
    // local meta checks the signature with. A fresh auth_date on every call, so it never expires. The BackButton is a flag and a log;
    // PressBackButton presses it (Play checks through execute_code).
    public class TelegramDebugAPI : ITelegramAPI
    {
        public TelegramDebugAPI(TelegramEditorOptions options, ILog<TelegramDebugAPI> log)
        {
            _options = options;
            _log = log;
        }

        private readonly TelegramEditorOptions _options;
        private readonly ILog _log;

        public const long UserId = 1001;
        public const string FirstName = "Editor";
        public const string LanguageCode = "ru";

        private int _backButtonPresses;

        public bool IsBackButtonVisible { get; private set; }

        public string GetInitData()
        {
#if UNITY_EDITOR
            var token = ReadBotToken(_options.BotTokenSettingsPath);
            var user = $"{{\"id\":{UserId},\"first_name\":\"{FirstName}\",\"language_code\":\"{LanguageCode}\"}}";
            var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            const string queryId = "AAH_editor_debug";

            // Sorted by key, "key=value" joined by \n: the data check string of the Bot API documentation.
            var dataCheckString = $"auth_date={authDate}\nquery_id={queryId}\nuser={user}";

            using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
            var secret = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(token));

            using var hashHmac = new HMACSHA256(secret);
            var hash = hashHmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            var hashHex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();

            return $"query_id={Uri.EscapeDataString(queryId)}&user={Uri.EscapeDataString(user)}" +
                   $"&auth_date={authDate}&hash={hashHex}";
#else
            throw new InvalidOperationException("[Telegram] The debug API is editor only");
#endif
        }

        public void SetBackButtonVisible(bool isVisible)
        {
            if (IsBackButtonVisible == isVisible)
                return;

            IsBackButtonVisible = isVisible;
            _log.Debug("BackButton visible: {IsVisible}", isVisible);
        }

        public int GetBackButtonPresses()
        {
            return _backButtonPresses;
        }

        public void PressBackButton()
        {
            if (IsBackButtonVisible == false)
                throw new InvalidOperationException("[Telegram] [Debug] BackButton is hidden, Telegram would not let it be pressed");

            _backButtonPresses++;
        }

#if UNITY_EDITOR
        // The path of the project (TelegramEditorOptions) is relative to the folder of the Unity project.
        private static string ReadBotToken(string settingsPath)
        {
            var projectFolder = Path.GetDirectoryName(Application.dataPath);
            var path = Path.GetFullPath(Path.Combine(projectFolder, settingsPath));

            if (File.Exists(path) == false)
            {
                throw new FileNotFoundException(
                    "[Telegram] [Debug] No local settings with the bot token (TelegramEditorOptions): put {\"TelegramBotToken\": \"<test token>\"} there and restart the cluster",
                    path);
            }

            var settings = JsonUtility.FromJson<LocalSettings>(File.ReadAllText(path));

            if (string.IsNullOrEmpty(settings.TelegramBotToken) == true)
                throw new InvalidOperationException("[Telegram] [Debug] TelegramBotToken is empty in " + path);

            return settings.TelegramBotToken;
        }

        [Serializable]
        private class LocalSettings
        {
            public string TelegramBotToken;
        }
#endif
    }
}
