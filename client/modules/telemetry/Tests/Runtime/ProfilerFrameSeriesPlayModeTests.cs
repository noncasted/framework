using System.Collections;
using Internal;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The series on a real ProfilerRecorder: every take sees the frames since the previous one, not only the first take.
    public class ProfilerFrameSeriesPlayModeTests
    {
        private const string MarkerName = "Telemetry.Tests.FrameSeries";

        private static readonly ProfilerMarker _marker = new ProfilerMarker(MarkerName);

        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        private static IEnumerator MarkedFrames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _marker.Begin();
                _marker.End();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Marker_SeveralTakes_EveryTakeHasFrames()
        {
            var series = new ProfilerTelemetryFrameSeriesSource().OpenMarker(_lifetime, MarkerName);

            for (var take = 1; take <= 3; take++)
            {
                yield return MarkedFrames(5);

                var frames = series.Take(out _, out _);

                Assert.Greater(frames, 0, $"take {take}");
            }
        }

        [UnityTest]
        public IEnumerator Counter_SeveralTakes_EveryTakeHasFrames()
        {
            var source = new ProfilerTelemetryFrameSeriesSource();

            Assert.IsTrue(source.TryOpenCounter(_lifetime, ProfilerCategory.Memory.Name, "GC Allocated In Frame", out var series));

            for (var take = 1; take <= 3; take++)
            {
                yield return MarkedFrames(5);

                var frames = series.Take(out _, out _);

                Assert.Greater(frames, 0, $"take {take}");
            }
        }
    }
}
