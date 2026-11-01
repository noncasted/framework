using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    // A vertical scroll view (the list of open lobbies) moves its content so that the selected row is seen.
    internal static class ScrollIntoSelection
    {
        public static void Apply(Selectable current)
        {
            var scroll = current.GetComponentInParent<ScrollRect>();

            if (scroll == null || scroll.vertical == false)
                return;

            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, current.transform);
            var view = viewport.rect;
            var shift = 0f;

            if (bounds.max.y > view.yMax)
                shift = view.yMax - bounds.max.y;
            else if (bounds.min.y < view.yMin)
                shift = view.yMin - bounds.min.y;

            if (shift == 0f)
                return;

            var content = scroll.content;
            content.anchoredPosition += new Vector2(0f, shift);
            scroll.velocity = Vector2.zero;
        }
    }
}
