using Internal;
using UnityEngine;

namespace Global.UI
{
    // Lays the targets out as if the screen were only the safe area, without a container in the scene: every frame the
    // safe area insets (whole units of the root canvas: a pixel UI stays on its grid) are added to the original place,
    // weighted by the anchors. An element anchored to the left edge moves right by the left inset, a stretched one
    // shrinks by both insets, a centred one moves by half of their difference. The anchors of a target are taken
    // against the screen: its parent must reach the edges the target is anchored to. A target anchored to a point
    // only moves, its size stays free (a fitter, a view); nothing else may write its position. Nothing may write the
    // offsets of a stretched target.
    public class SafeAreaPadding : IUpdatable
    {
        public SafeAreaPadding(IUpdater updater, ISafeArea safeArea, params RectTransform[] targets)
        {
            _updater = updater;
            _safeArea = safeArea;
            _targets = targets;
            _offsetsMin = new Vector2[targets.Length];
            _offsetsMax = new Vector2[targets.Length];
            _positions = new Vector2[targets.Length];
            _applied = new Vector4[targets.Length];
        }

        private readonly IUpdater _updater;
        private readonly ISafeArea _safeArea;
        private readonly RectTransform[] _targets;
        private readonly Vector2[] _offsetsMin;
        private readonly Vector2[] _offsetsMax;
        private readonly Vector2[] _positions;
        private readonly Vector4[] _applied;

        public void Run(IReadOnlyLifetime lifetime)
        {
            for (var i = 0; i < _targets.Length; i++)
            {
                _offsetsMin[i] = _targets[i].offsetMin;
                _offsetsMax[i] = _targets[i].offsetMax;
                _positions[i] = _targets[i].anchoredPosition;
                _applied[i] = new Vector4(float.NaN, 0f, 0f, 0f);
            }

            OnUpdate(0f);
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            var area = _safeArea.Area.Value;
            var screen = new Vector2(Screen.width, Screen.height);

            // Fractions of the screen: left, bottom, right, top.
            var left = area.xMin / screen.x;
            var bottom = area.yMin / screen.y;
            var right = (screen.x - area.xMax) / screen.x;
            var top = (screen.y - area.yMax) / screen.y;

            for (var i = 0; i < _targets.Length; i++)
            {
                var target = _targets[i];
                var root = ((RectTransform)target.root).rect.size;
                var insets = new Vector4(
                    Mathf.Ceil(left * root.x),
                    Mathf.Ceil(bottom * root.y),
                    Mathf.Ceil(right * root.x),
                    Mathf.Ceil(top * root.y));

                if (insets == _applied[i])
                    continue;

                _applied[i] = insets;

                var anchorMin = target.anchorMin;
                var anchorMax = target.anchorMax;

                if (anchorMin == anchorMax)
                {
                    target.anchoredPosition = _positions[i] + Shift(insets, anchorMin);
                    continue;
                }

                target.offsetMin = _offsetsMin[i] + Shift(insets, anchorMin);
                target.offsetMax = _offsetsMax[i] + Shift(insets, anchorMax);
            }
        }

        // A point at the anchor a of the full screen moves to the anchor a of the safe area:
        // x = left + a * (width - left - right) = a * width + left * (1 - a) - right * a.
        private static Vector2 Shift(Vector4 insets, Vector2 anchor)
        {
            return new Vector2(
                insets.x * (1f - anchor.x) - insets.z * anchor.x,
                insets.y * (1f - anchor.y) - insets.w * anchor.y);
        }
    }
}
