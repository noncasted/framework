using UnityEngine;

namespace Global.UI
{
    // The insets of the safe area in units of a root and the move they give a point at an anchor.
    public static class SafeAreaInsets
    {
        // Left, bottom, right, top: the fractions of the screen outside the area times the size of the root, rounded
        // up to whole units.
        public static Vector4 Of(Rect area, Vector2Int screen, Vector2 root)
        {
            var left = area.xMin / screen.x;
            var bottom = area.yMin / screen.y;
            var right = (screen.x - area.xMax) / screen.x;
            var top = (screen.y - area.yMax) / screen.y;

            return new Vector4(
                Mathf.Ceil(left * root.x),
                Mathf.Ceil(bottom * root.y),
                Mathf.Ceil(right * root.x),
                Mathf.Ceil(top * root.y));
        }

        // A point at the anchor a of the full screen moves to the anchor a of the safe area:
        // x = left + a * (width - left - right) = a * width + left * (1 - a) - right * a.
        public static Vector2 Shift(Vector4 insets, Vector2 anchor)
        {
            return new Vector2(
                insets.x * (1f - anchor.x) - insets.z * anchor.x,
                insets.y * (1f - anchor.y) - insets.w * anchor.y);
        }
    }
}
