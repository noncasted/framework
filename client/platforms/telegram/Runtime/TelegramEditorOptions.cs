namespace Global.Publisher.Telegram
{
    // The Telegram platform in the editor (TelegramDebugAPI): where the project keeps the bot token its local meta
    // checks the init data with. A json file with {"TelegramBotToken": "<test token>"} (the local settings of the
    // backend of the project), the path relative to the folder of the Unity project.
    public class TelegramEditorOptions
    {
        public TelegramEditorOptions(string botTokenSettingsPath)
        {
            BotTokenSettingsPath = botTokenSettingsPath;
        }

        public string BotTokenSettingsPath { get; }
    }
}
