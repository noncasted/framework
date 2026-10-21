using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;

namespace Global.Web
{
    // The loading screen from index.html: it is visible from the page opening while Unity is still downloading,
    // and is hidden only by a call from the game.
    public class WebLoadingScreen : ILoadingScreen
    {
        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebLoadingScreenShow();

        [DllImport("__Internal")]
        private static extern void WebLoadingScreenShowInstantly();

        [DllImport("__Internal")]
        private static extern void WebLoadingScreenHide();

        [DllImport("__Internal")]
        private static extern bool WebLoadingScreenIsShown();

        [DllImport("__Internal")]
        private static extern bool WebLoadingScreenIsHidden();
#else
        private static void WebLoadingScreenShow() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static void WebLoadingScreenShowInstantly() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static void WebLoadingScreenHide() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static bool WebLoadingScreenIsShown() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static bool WebLoadingScreenIsHidden() =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");
#endif

        public async UniTask Show()
        {
            WebLoadingScreenShow();
            await UniTask.WaitUntil(() => WebLoadingScreenIsShown() == true);
        }

        public void ShowInstantly()
        {
            WebLoadingScreenShowInstantly();
        }

        public void Hide()
        {
            WebLoadingScreenHide();
        }

        public UniTask WaitHidden(IReadOnlyLifetime lifetime)
        {
            return UniTask.WaitUntil(() => WebLoadingScreenIsHidden() == true, cancellationToken: lifetime.Token);
        }
    }
}
