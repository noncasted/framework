using Internal;
using UnityEngine;

namespace Global.UI
{
    // The part of the screen not hidden by a notch, rounded corners or system bars, in screen pixels with the origin
    // at the bottom left (as Screen.safeArea). Changes with the screen size and the orientation.
    // Web: insets of the page (env(safe-area-inset-*), or Telegram's safeAreaInset + contentSafeAreaInset that the
    // Telegram template feeds into web-shell.js) through WebShell.jslib. Editor and native: Screen.safeArea
    // (the Device Simulator emulates it).
    public interface ISafeArea
    {
        IViewableProperty<Rect> Area { get; }
    }
}
