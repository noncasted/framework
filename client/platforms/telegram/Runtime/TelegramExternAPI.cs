using System.Runtime.InteropServices;

namespace Global.Publisher.Telegram
{
    public class TelegramExternAPI : ITelegramAPI
    {
        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string TelegramInitData();

        [DllImport("__Internal")]
        private static extern string TelegramLanguageCode();

        [DllImport("__Internal")]
        private static extern void TelegramSetBackButtonVisible(bool isVisible);

        [DllImport("__Internal")]
        private static extern int TelegramBackButtonPresses();
#else
        private static string TelegramInitData() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static string TelegramLanguageCode() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static void TelegramSetBackButtonVisible(bool isVisible) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int TelegramBackButtonPresses() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");
#endif

        public string GetInitData()
        {
            return TelegramInitData();
        }

        public string GetLanguageCode()
        {
            return TelegramLanguageCode();
        }

        public void SetBackButtonVisible(bool isVisible)
        {
            TelegramSetBackButtonVisible(isVisible);
        }

        public int GetBackButtonPresses()
        {
            return TelegramBackButtonPresses();
        }
    }
}
