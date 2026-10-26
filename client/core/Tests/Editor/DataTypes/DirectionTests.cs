using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    public class DirectionTests
    {
        [Test]
        public void RotateClamped_AcrossZero_TakesTheShortWay()
        {
            var source = 350f.ToAngle().ToVector2();
            var target = 10f.ToAngle().ToVector2();

            var rotated = source.RotateClamped(target, 15f);

            Assert.AreEqual(5f, rotated.ToAngle(), 0.01f);
        }

        [Test]
        public void RotateClamped_WithinTheClamp_ReachesTheTarget()
        {
            var source = 10f.ToAngle().ToVector2();
            var target = 350f.ToAngle().ToVector2();

            var rotated = source.RotateClamped(target, 45f);

            Assert.AreEqual(350f, rotated.ToAngle(), 0.01f);
        }

        [TestCase(-400f, 320f)]
        [TestCase(-360f, 0f)]
        [TestCase(-90f, 270f)]
        [TestCase(0f, 0f)]
        [TestCase(360f, 0f)]
        [TestCase(725f, 5f)]
        public void ToAngle_WrapsIntoZeroTo360(float value, float expected)
        {
            Assert.AreEqual(expected, value.ToAngle().Value, 0.001f);
        }

        [TestCase(0.001f, 1f, "Up")]
        [TestCase(-0.001f, -1f, "Down")]
        [TestCase(1f, 0.2f, "Right")]
        [TestCase(-1f, -0.2f, "Left")]
        public void ToDirection4_TakesTheDominantAxis(float x, float y, string expected)
        {
            Assert.AreEqual(expected, new Vector2(x, y).ToDirection4().ToString());
        }

        [TestCase(0f, 1f, "Up")]
        [TestCase(1f, 1f, "Up_Right")]
        [TestCase(1f, 0f, "Right")]
        [TestCase(1f, -1f, "Down_Right")]
        [TestCase(0f, -1f, "Down")]
        [TestCase(-1f, -1f, "Down_Left")]
        [TestCase(-1f, 0f, "Left")]
        [TestCase(-1f, 1f, "Up_Left")]
        [TestCase(1f, 0.1f, "Right")]
        [TestCase(0.1f, 1f, "Up")]
        public void ToDirection8_TakesTheNearestOfEightDirections(float x, float y, string expected)
        {
            Assert.AreEqual(expected, new Vector2(x, y).ToDirection8().ToString());
        }

        [Test]
        public void ToDirection_ZeroVector_Throws()
        {
            Assert.Throws<ArgumentException>(() => Vector2.zero.ToDirection4());
            Assert.Throws<ArgumentException>(() => Vector2.zero.ToDirection8());
        }

        [Test]
        public void Random_EmptyList_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new List<int>().Random());
        }
    }
}
