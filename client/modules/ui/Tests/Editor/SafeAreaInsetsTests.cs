using NUnit.Framework;
using UnityEngine;

namespace Global.UI.Tests
{
    public class SafeAreaInsetsTests
    {
        // A phone in landscape, 2400 x 1080 pixels with a notch of 120 on the left and a bar of 63 at the bottom, on a
        // canvas of 600 x 270 units: 30 and 15.75 -> 16 units.
        [Test]
        public void Of_ScalesFractionsToRootRoundedUp()
        {
            var area = Rect.MinMaxRect(120f, 63f, 2400f, 1080f);

            var insets = SafeAreaInsets.Of(area, new Vector2Int(2400, 1080), new Vector2(600f, 270f));

            Assert.That(insets, Is.EqualTo(new Vector4(30f, 16f, 0f, 0f)));
        }

        [Test]
        public void Of_FullScreen_IsZero()
        {
            var insets = SafeAreaInsets.Of(new Rect(0f, 0f, 895f, 393f), new Vector2Int(895, 393), new Vector2(447f, 196f));

            Assert.That(insets, Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void Shift_WeighsInsetsByAnchor()
        {
            var insets = new Vector4(10f, 20f, 30f, 40f);

            Assert.That(SafeAreaInsets.Shift(insets, Vector2.zero), Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(SafeAreaInsets.Shift(insets, Vector2.one), Is.EqualTo(new Vector2(-30f, -40f)));
            Assert.That(SafeAreaInsets.Shift(insets, new Vector2(0.5f, 0.5f)), Is.EqualTo(new Vector2(-10f, -10f)));
        }
    }
}
