using Internal;
using UnityEngine;

namespace Global.UI
{
    // Lays the targets out as if the screen were only the safe area, without a container in the scene: the safe area
    // insets (whole units of the root canvas: a pixel UI stays on its grid) are added to the original place, weighted
    // by the anchors. An element anchored to the left edge moves right by the left inset, a stretched one shrinks by
    // both insets, a centred one moves by half of their difference. The anchors of a target are taken against the
    // screen: its parent must reach the edges the target is anchored to. A target anchored to a point only moves, its
    // size stays free (a fitter, a view); nothing else may write its position. Nothing may write the offsets of a
    // stretched target. The root of a target is taken at Run: a target stays under it.
    // Recomputed only on a change: the safe area is a subscription; the screen and the size of the root have no event,
    // so a frame compares them with the last ones (Screen.width / height and the size delta of the root, which is its
    // rect for a transform without a parent: RectTransform.rect would lay the anchors out again every frame).
    public class SafeAreaPadding : IUpdatable
    {
        public SafeAreaPadding(IUpdater updater, ISafeArea safeArea, params RectTransform[] targets)
        {
            _updater = updater;
            _safeArea = safeArea;
            _targets = targets;
            _roots = new RectTransform[targets.Length];
            _rootSizes = new Vector2[targets.Length];
            _offsetsMin = new Vector2[targets.Length];
            _offsetsMax = new Vector2[targets.Length];
            _positions = new Vector2[targets.Length];
            _applied = new Vector4[targets.Length];
        }

        private readonly IUpdater _updater;
        private readonly ISafeArea _safeArea;
        private readonly RectTransform[] _targets;
        private readonly RectTransform[] _roots;
        private readonly Vector2[] _rootSizes;
        private readonly Vector2[] _offsetsMin;
        private readonly Vector2[] _offsetsMax;
        private readonly Vector2[] _positions;
        private readonly Vector4[] _applied;

        private bool _areaChanged;
        private Vector2Int _screen;

        public void Run(IReadOnlyLifetime lifetime)
        {
            for (var i = 0; i < _targets.Length; i++)
            {
                _roots[i] = (RectTransform)_targets[i].root;
                _rootSizes[i] = new Vector2(float.NaN, 0f);
                _offsetsMin[i] = _targets[i].offsetMin;
                _offsetsMax[i] = _targets[i].offsetMax;
                _positions[i] = _targets[i].anchoredPosition;
                _applied[i] = new Vector4(float.NaN, 0f, 0f, 0f);
            }

            _safeArea.Area.Advise(lifetime, (_, _) => _areaChanged = true);
            _areaChanged = true;
            OnUpdate(0f);
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            var changed = _areaChanged == true || screen != _screen;
            _areaChanged = false;
            _screen = screen;

            for (var i = 0; i < _targets.Length; i++)
            {
                var root = _roots[i].sizeDelta;

                if (changed == false && root == _rootSizes[i])
                    continue;

                _rootSizes[i] = root;
                Apply(i, SafeAreaInsets.Of(_safeArea.Area.Value, screen, root));
            }
        }

        private void Apply(int index, Vector4 insets)
        {
            if (insets == _applied[index])
                return;

            _applied[index] = insets;

            var target = _targets[index];
            var anchorMin = target.anchorMin;
            var anchorMax = target.anchorMax;

            if (anchorMin == anchorMax)
            {
                target.anchoredPosition = _positions[index] + SafeAreaInsets.Shift(insets, anchorMin);
                return;
            }

            target.offsetMin = _offsetsMin[index] + SafeAreaInsets.Shift(insets, anchorMin);
            target.offsetMax = _offsetsMax[index] + SafeAreaInsets.Shift(insets, anchorMax);
        }
    }
}
