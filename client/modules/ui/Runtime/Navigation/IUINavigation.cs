using Internal;
using UnityEngine;

namespace Global.UI
{
    // The selection of the uGUI buttons by the gamepad. A screen or a window adds its root while it is open;
    // the window on top is the last added one whose root is active in the hierarchy (a panel opened in place of
    // another one hides it, a window over a screen is added after it).
    public interface IUINavigation
    {
        void Add(IReadOnlyLifetime lifetime, RectTransform root);

        // The first selection is looked for under start first (a part of the root: the buttons of the lobby, not the
        // room code that comes before them in the hierarchy), then in the whole root.
        void Add(IReadOnlyLifetime lifetime, RectTransform root, RectTransform start);

        // A window over every other one, also over those added after it (the notice of an outdated client).
        void AddOverlay(IReadOnlyLifetime lifetime, RectTransform root);
    }
}
