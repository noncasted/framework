using Internal;
using NUnit.Framework;
using UnityEngine;

namespace Global.UI.Tests
{
    // SafeAreaPadding against a root of a fixed size (a world space canvas: its rect is its size delta, not the
    // screen), the safe area given in fractions of the screen of the run. The updater is not run: the tests call
    // OnUpdate as frames.
    public class SafeAreaPaddingTests
    {
        private Lifetime _lifetime;
        private ViewableProperty<Rect> _area;
        private GameObject _root;
        private RectTransform _rootTransform;

        [SetUp]
        public void SetUp()
        {
            Assume.That(Screen.width > 0 && Screen.height > 0, "the run has no screen");

            // NUnit keeps one instance of the fixture for all its tests: a lifetime and an area per test.
            _lifetime = new Lifetime();
            _area = new ViewableProperty<Rect>();

            _root = new GameObject("root", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _rootTransform = (RectTransform)_root.transform;
            _rootTransform.sizeDelta = new Vector2(400f, 200f);
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            Object.DestroyImmediate(_root);
        }

        // Fractions of the screen cut by the safe area: left, bottom, right, top.
        private void SetArea(float left, float bottom, float right, float top)
        {
            var width = Screen.width;
            var height = Screen.height;
            _area.Set(Rect.MinMaxRect(left * width, bottom * height, width - right * width, height - top * height));
        }

        private RectTransform Stretched()
        {
            var target = new GameObject("stretched", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(_rootTransform, false);
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = new Vector2(2f, 3f);
            target.offsetMax = new Vector2(-4f, -5f);
            return target;
        }

        private RectTransform Corner()
        {
            var target = new GameObject("corner", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(_rootTransform, false);
            target.anchorMin = new Vector2(1f, 1f);
            target.anchorMax = new Vector2(1f, 1f);
            target.sizeDelta = new Vector2(30f, 20f);
            target.anchoredPosition = new Vector2(-10f, -6f);
            return target;
        }

        private SafeAreaPadding Run(params RectTransform[] targets)
        {
            var padding = new SafeAreaPadding(new ManualUpdater(), new FakeSafeArea(_area), targets);
            padding.Run(_lifetime);
            return padding;
        }

        // The insets are whole units of the root, rounded up: 1/8 of 400 = 50, 1/16 of 200 = 12.5 -> 13, 1/32 of
        // 400 = 12.5 -> 13, 1/64 of 200 = 3.125 -> 4 (fractions exact in floats: no rounding noise under the ceiling).
        [Test]
        public void Run_AppliesInsetsWeightedByAnchors()
        {
            SetArea(0.125f, 0.0625f, 0.03125f, 0.015625f);
            var stretched = Stretched();
            var corner = Corner();

            Run(stretched, corner);

            Assert.That(stretched.offsetMin, Is.EqualTo(new Vector2(2f + 50f, 3f + 13f)));
            Assert.That(stretched.offsetMax, Is.EqualTo(new Vector2(-4f - 13f, -5f - 4f)));
            Assert.That(corner.anchoredPosition, Is.EqualTo(new Vector2(-10f - 13f, -6f - 4f)));
            Assert.That(corner.sizeDelta, Is.EqualTo(new Vector2(30f, 20f)));
        }

        // A centred target moves by half of the difference of the insets.
        [Test]
        public void Run_MovesCentredTargetByHalfTheDifference()
        {
            SetArea(0.125f, 0f, 0f, 0f);
            var target = Corner();
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = Vector2.zero;

            Run(target);

            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(25f, 0f)));
        }

        [Test]
        public void AreaChange_RecomputesOnTheNextFrame()
        {
            SetArea(0.125f, 0f, 0f, 0f);
            var target = Stretched();
            var padding = Run(target);

            SetArea(0f, 0f, 0.0625f, 0f);
            padding.OnUpdate(0f);

            Assert.That(target.offsetMin, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(target.offsetMax, Is.EqualTo(new Vector2(-4f - 25f, -5f)));
        }

        // The size of the root (the canvas on a resized screen, a new scale) changes the insets in units.
        [Test]
        public void RootResize_RecomputesOnTheNextFrame()
        {
            SetArea(0.125f, 0f, 0f, 0f);
            var target = Stretched();
            var padding = Run(target);

            _rootTransform.sizeDelta = new Vector2(800f, 200f);
            padding.OnUpdate(0f);

            Assert.That(target.offsetMin, Is.EqualTo(new Vector2(2f + 100f, 3f)));
        }

        // Without a change a frame writes nothing: a value set over the padding stays.
        [Test]
        public void NoChange_WritesNothing()
        {
            SetArea(0.125f, 0f, 0f, 0f);
            var target = Corner();
            var padding = Run(target);

            target.anchoredPosition = new Vector2(1f, 1f);
            padding.OnUpdate(0f);
            padding.OnUpdate(0f);

            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(1f, 1f)));
        }

        // Back to the first area: the original place, no drift from the frames between.
        [Test]
        public void AreaChangeAndBack_RestoresOriginalPlace()
        {
            SetArea(0.125f, 0.0625f, 0.03125f, 0.015625f);
            var target = Corner();
            var padding = Run(target);

            SetArea(0f, 0f, 0f, 0f);
            padding.OnUpdate(0f);

            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(-10f, -6f)));

            SetArea(0.125f, 0.0625f, 0.03125f, 0.015625f);
            padding.OnUpdate(0f);

            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(-23f, -10f)));
        }

        private class ManualUpdater : IUpdater
        {
            public IViewableProperty<float> Speed { get; } = new ViewableProperty<float>(1f);

            public void SetSpeed(float speed)
            {
            }

            public void Pause()
            {
            }

            public void Continue()
            {
            }

            public void Add(IReadOnlyLifetime l, IUpdatable u)
            {
            }

            public void Add(IReadOnlyLifetime l, IPreUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u)
            {
            }

            public void Add(IReadOnlyLifetime l, IFixedUpdatable u)
            {
            }

            public void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, IPostUpdatable updatable)
            {
            }

            public void Add(IReadOnlyLifetime l, ILateUpdatable updatable)
            {
            }
        }

        private class FakeSafeArea : ISafeArea
        {
            public FakeSafeArea(IViewableProperty<Rect> area)
            {
                Area = area;
            }

            public IViewableProperty<Rect> Area { get; }
        }
    }
}
