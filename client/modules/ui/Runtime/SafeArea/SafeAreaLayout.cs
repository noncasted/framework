using Internal;
using UnityEngine;

namespace Global.UI
{
    public static class SafeAreaLayout
    {
        // Keeps the target inside the safe area: its anchors follow ISafeArea, the offsets are zeroed.
        // The parent of the target must cover the whole screen (the root of a screen space canvas or a stretched
        // child of it); the content of the target lays out against its edges as before.
        public static void Fit(IReadOnlyLifetime lifetime, ISafeArea safeArea, RectTransform target)
        {
            safeArea.Area.View(lifetime, area => Apply(target, area));
        }

        public static void Apply(RectTransform target, Rect area)
        {
            var screen = new Vector2(Screen.width, Screen.height);

            target.anchorMin = area.min / screen;
            target.anchorMax = area.max / screen;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
