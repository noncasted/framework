using System.Collections;
using System.Diagnostics;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The markers in the real player loop, next to the runners of UniTask, and the cost of the collectors of every frame.
    public class FramePhasesPlayModeTests
    {
        private Lifetime _running;

        [SetUp]
        public void SetUp()
        {
            _running = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _running.Terminate();
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Loop_RealFrames_PhasesFixedStepsAndRemoval()
        {
            var phases = new FramePhasesTelemetryCollector();
            var clock = new FramePhaseClock(new TelemetryTicks(), new IFramePhaseListener[] { phases });
            var loop = new FramePhaseLoop(clock);
            var sample = new RecordingSample();
            var fixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 0.005f;

            phases.Start(_running);
            loop.Start(_running);
            Assert.AreEqual(FramePhases.Count * 2, FramePhaseLoopEdit.CountMarkers(PlayerLoop.GetCurrentPlayerLoop()));

            yield return Frames(30);
            phases.Collect(sample);
            Time.fixedDeltaTime = fixedDelta;

            foreach (var name in FramePhases.Names)
                Assert.IsTrue(sample.Values["frame.phase." + name] >= 0d, name);

            Assert.Greater(sample.Values["frame.phase.postlate"], 0d);
            Assert.Greater(sample.Values["frame.fixed_steps"], 0d);

            _running.Terminate();
            Assert.AreEqual(0, FramePhaseLoopEdit.CountMarkers(PlayerLoop.GetCurrentPlayerLoop()));

            // The loop goes on without the markers.
            var frame = Time.frameCount;
            yield return null;
            Assert.Greater(Time.frameCount, frame);
        }

        [UnityTest]
        public IEnumerator Settings_And_RenderSize_RealSources()
        {
            var sample = new RecordingSample();

            new SettingsTelemetryCollector().Collect(sample);
            new RenderSizeTelemetryCollector(new UnityTelemetryRenderSize()).Collect(sample);
            yield return null;

            Assert.AreEqual(Time.timeScale, sample.Values["settings.time_scale"]);
            Assert.AreEqual(QualitySettings.GetQualityLevel(), sample.Values["settings.quality"]);
            Assert.IsFalse(sample.Values.ContainsKey("render.canvas_width"));
        }

        // The work of the markers and of the listeners of every platform per frame, without the loop around them. The timer of
        // the GPU (WebGL only) adds a call to the page in a few frames of 30.
        [Test]
        public void Cost_PerFrame_UnderTenMicroseconds()
        {
            var buffer = new TelemetryBuffer(new ClientTelemetryOptions(), TelemetryMemoryTraceStore.Trace(new TelemetryNameTable(new TelemetryNameGuard(true))));
            buffer.Open();
            var listeners = new IFramePhaseListener[]
            {
                new FramePhasesTelemetryCollector(),
                new FrameSpikesTelemetryCollector(buffer, new TelemetryClock(), new NoTelemetryPage()),
                new GcAllocationsTelemetryCollector(new UnityTelemetryMemoryReadings())
            };

            foreach (var listener in listeners)
                ((ITelemetryLiveCollector)listener).Start(_running);

            var clock = new FramePhaseClock(new TelemetryTicks(), listeners);
            clock.Reset();
            const int frames = 20000;
            var stopwatch = Stopwatch.StartNew();

            for (var frame = 0; frame < frames; frame++)
            {
                for (var phase = 0; phase < FramePhases.Count; phase++)
                {
                    clock.Begin(phase);
                    clock.End(phase);
                }
            }

            var microseconds = stopwatch.Elapsed.TotalMilliseconds * 1000d / frames;
            TestContext.WriteLine($"frame collectors: {microseconds:F2} us per frame");
            Assert.Less(microseconds, 10d);
        }
    }
}
