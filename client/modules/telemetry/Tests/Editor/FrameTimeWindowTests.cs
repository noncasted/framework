using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    public class FrameTimeWindowTests
    {
        private const double _tolerance = 0.001;

        private static FrameTimeStats Take(params float[] seconds)
        {
            var window = new FrameTimeWindow();

            foreach (var dt in seconds)
                window.Add(dt);

            Assert.IsTrue(window.TryTake(out var stats));
            return stats;
        }

        [Test]
        public void Take_GivenDeltas_AllAggregates()
        {
            var stats = Take(0.010f, 0.020f, 0.030f, 0.040f, 0.100f);

            Assert.AreEqual(5, stats.Frames);
            Assert.AreEqual(40.0, stats.AverageMs, _tolerance);
            Assert.AreEqual(30.0, stats.P50Ms, _tolerance);
            Assert.AreEqual(100.0, stats.P95Ms, _tolerance);
            Assert.AreEqual(100.0, stats.MaxMs, _tolerance);
            Assert.AreEqual(25.0, stats.Fps, _tolerance);
            Assert.AreEqual(2, stats.Over33);
            Assert.AreEqual(1, stats.Over50);
        }

        [Test]
        public void Take_HundredFrames_NearestRankPercentiles()
        {
            var window = new FrameTimeWindow();

            // Out of order: the percentiles do not depend on the order of the frames.
            for (var i = 100; i >= 1; i--)
                window.Add(i * 0.001f);

            Assert.IsTrue(window.TryTake(out var stats));
            Assert.AreEqual(50.0, stats.P50Ms, _tolerance);
            Assert.AreEqual(95.0, stats.P95Ms, _tolerance);
            Assert.AreEqual(100.0, stats.MaxMs, _tolerance);
        }

        [Test]
        public void Take_OneFrame_EveryStatIsIt()
        {
            var stats = Take(0.020f);

            Assert.AreEqual(20.0, stats.AverageMs, _tolerance);
            Assert.AreEqual(20.0, stats.P50Ms, _tolerance);
            Assert.AreEqual(20.0, stats.P95Ms, _tolerance);
            Assert.AreEqual(50.0, stats.Fps, _tolerance);
        }

        [Test]
        public void Over_ExactThresholdIsNotOver()
        {
            var stats = Take(0.033f, 0.050f, 0.0331f, 0.0501f);

            Assert.AreEqual(3, stats.Over33);
            Assert.AreEqual(1, stats.Over50);
        }

        [Test]
        public void Take_NoFrames_False()
        {
            Assert.IsFalse(new FrameTimeWindow().TryTake(out _));
        }

        // The first frame of the player has a delta of zero: it is not a frame of infinite speed.
        [Test]
        public void Add_ZeroDelta_NotCounted()
        {
            var window = new FrameTimeWindow();

            window.Add(0f);

            Assert.IsFalse(window.TryTake(out _));
        }

        [Test]
        public void Take_StartsANewWindow()
        {
            var window = new FrameTimeWindow();

            window.Add(0.100f);
            window.TryTake(out _);
            window.Add(0.010f);

            Assert.IsTrue(window.TryTake(out var stats));
            Assert.AreEqual(1, stats.Frames);
            Assert.AreEqual(10.0, stats.MaxMs, _tolerance);
            Assert.AreEqual(0, stats.Over33);
            Assert.IsFalse(window.TryTake(out _));
        }

        // Over the capacity the percentiles come from the frames kept, the counters still see everything.
        [Test]
        public void Add_OverCapacity_NoThrow_CountersStayExact()
        {
            var window = new FrameTimeWindow();

            for (var i = 0; i < FrameTimeWindow.Capacity + 10; i++)
                window.Add(0.040f);

            Assert.IsTrue(window.TryTake(out var stats));
            Assert.AreEqual(FrameTimeWindow.Capacity + 10, stats.Frames);
            Assert.AreEqual(FrameTimeWindow.Capacity + 10, stats.Over33);
            Assert.AreEqual(40.0, stats.P95Ms, _tolerance);
        }
    }
}
