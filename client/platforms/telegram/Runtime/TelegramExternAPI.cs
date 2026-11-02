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
        private static extern void TelegramSetBackButtonVisible(bool isVisible);

        [DllImport("__Internal")]
        private static extern int TelegramBackButtonPresses();
#else
        private static string TelegramInitData() =>
            throw Internal.JsLibStub.Missing();

        private static void TelegramSetBackButtonVisible(bool isVisible) =>
            throw Internal.JsLibStub.Missing();

        private static int TelegramBackButtonPresses() =>
            throw Internal.JsLibStub.Missing();
#endif

        public string GetInitData()
        {
            return TelegramInitData();
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
