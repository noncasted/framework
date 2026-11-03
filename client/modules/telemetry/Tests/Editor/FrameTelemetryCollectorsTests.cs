using System.Diagnostics;
using System.IO;
using System.Linq;
using Internal;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The collectors of every frame on a manual clock of ticks: the phases, the spikes, the allocations, the timer of the GPU.
    public class FrameTelemetryCollectorsTests
    {
        private ManualTicks _ticks;
        private Lifetime _running;

        [SetUp]
        public void SetUp()
        {
            _ticks = new ManualTicks();
            _running = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _running.Terminate();
        }

        private FramePhaseClock Clock(params IFramePhaseListener[] listeners)
        {
            var clock = new FramePhaseClock(_ticks, listeners);
            clock.Reset();
            return clock;
        }

        // A frame with the given milliseconds per phase, the fixed one run `steps` times.
        private void Frame(FramePhaseClock clock, double[] phases, int steps = 1, double before = 0d)
        {
            _ticks.Advance(before);

            for (var phase = 0; phase < FramePhases.Count; phase++)
            {
                var runs = phase == FramePhases.Fixed ? steps : 1;

                for (var run = 0; run < runs; run++)
                {
                    clock.Begin(phase);
                    _ticks.Advance(phases[phase] / runs);
                    clock.End(phase);
                }
            }
        }

        private static double[] Phases(double early = 1d, double fix = 2d, double pre = 0.5d, double update = 5d, double preLate = 1d, double postLate = 3d)
        {
            return new[] { early, fix, pre, update, preLate, postLate };
        }

        [Test]
        public void Phases_TwoFrames_AverageAndMaxPerPhase()
        {
            var collector = new FramePhasesTelemetryCollector();
            collector.Start(_running);
            var clock = Clock(collector);
            var sample = new RecordingSample();

            Frame(clock, Phases(update: 4d), steps: 2);
            Frame(clock, Phases(update: 8d), steps: 0);
            collector.Collect(sample);

            Assert.AreEqual(6d, sample.Values["frame.phase.update"], 1e-9);
            Assert.AreEqual(8d, sample.Values["frame.phase.update.max"], 1e-9);
            Assert.AreEqual(1d, sample.Values["frame.phase.early"], 1e-9);
            Assert.AreEqual(1d, sample.Values["frame.phase.fixed"], 1e-9);
            Assert.AreEqual(1d, sample.Values["frame.fixed_steps"], 1e-9);
            Assert.AreEqual(2d, sample.Values["frame.fixed_steps.max"], 1e-9);
            Assert.AreEqual(14, sample.Values.Count);
        }

        [Test]
        public void Phases_NoFrame_NoValues()
        {
            var collector = new FramePhasesTelemetryCollector();
            collector.Start(_running);
            var sample = new RecordingSample();

            collector.Collect(sample);

            Assert.IsEmpty(sample.Values);
        }

        // A session that opens in the middle of a frame starts with the next one.
        [Test]
        public void Clock_StartsMidFrame_SkipsThePartOfThatFrame()
        {
            var collector = new FramePhasesTelemetryCollector();
            collector.Start(_running);
            var clock = Clock(collector);
            var sample = new RecordingSample();

            clock.Begin(FramePhases.Update);
            clock.End(FramePhases.Update);
            clock.Begin(FramePhases.PostLate);
            clock.End(FramePhases.PostLate);
            collector.Collect(sample);
            Assert.IsEmpty(sample.Values);

            Frame(clock, Phases());
            collector.Collect(sample);
            Assert.AreEqual(5d, sample.Values["frame.phase.update"], 1e-9);
        }

        [Test]
        public void Clock_ThrowingListener_OthersGoOn()
        {
            var collector = new FramePhasesTelemetryCollector();
            collector.Start(_running);
            var clock = Clock(new ThrowingFrameListener(), collector);
            var sample = new RecordingSample();

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Frame(clock, Phases());
            Frame(clock, Phases());
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            collector.Collect(sample);

            Assert.AreEqual(5d, sample.Values["frame.phase.update"], 1e-9);
        }

        [Test]
        public void Spikes_LongFrame_TwoMarksWithTheLongestPhase()
        {
            var fixture = new TelemetryFixture();
            var spikes = new FrameSpikesTelemetryCollector(fixture.Buffer, fixture.Clock, fixture.Page);
            fixture.Buffer.Open();
            spikes.Start(_running);
            var clock = Clock(spikes);
            var sample = new RecordingSample();

            Frame(clock, Phases());
            Frame(clock, Phases(update: 300d));
            Frame(clock, Phases(), before: 400d);
            Frame(clock, Phases());
            spikes.Collect(sample);

            var marks = Enumerable.Range(0, fixture.Buffer.Marks.Count).Select(i => fixture.Buffer.Marks.At(i).Name).ToArray();
            CollectionAssert.AreEqual(new[] { "frame.spike", "frame.spike.update", "frame.spike", "frame.spike.browser" }, marks);
            Assert.AreEqual(2d, sample.Values["frame.spikes"]);

            spikes.Collect(sample);
            Assert.AreEqual(0d, sample.Values["frame.spikes"]);
        }

        [Test]
        public void Spikes_OverTheLimit_CountedNotMarked()
        {
            var fixture = new TelemetryFixture();
            var spikes = new FrameSpikesTelemetryCollector(fixture.Buffer, fixture.Clock, fixture.Page);
            fixture.Buffer.Open();
            spikes.Start(_running);
            var clock = Clock(spikes);
            var sample = new RecordingSample();
            Frame(clock, Phases());

            for (var i = 0; i < FrameSpikesTelemetryCollector.MaxMarked + 10; i++)
            {
                Frame(clock, Phases(), before: 300d);
                fixture.Buffer.Marks.RemoveOldest(fixture.Buffer.Marks.Count);
            }

            spikes.Collect(sample);
            Assert.AreEqual(FrameSpikesTelemetryCollector.MaxMarked + 10, sample.Values["frame.spikes"]);

            Frame(clock, Phases(), before: 300d);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
        }

        [Test]
        public void Spikes_FrameAfterAHiddenTab_NoSpike()
        {
            var fixture = new TelemetryFixture();
            var spikes = new FrameSpikesTelemetryCollector(fixture.Buffer, fixture.Clock, fixture.Page);
            fixture.Buffer.Open();
            spikes.Start(_running);
            var clock = Clock(spikes);
            Frame(clock, Phases());

            fixture.Page.Hide();
            fixture.Page.Show();
            Frame(clock, Phases(), before: 5000d);

            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
        }

        [Test]
        public void Allocations_GrowthPerFrame_CollectionAddsNothing()
        {
            var readings = new FakeMemoryReadings();
            readings.Set(0, 0, 1000, 0, 0);
            var collector = new GcAllocationsTelemetryCollector(readings);
            collector.Start(_running);
            var clock = Clock(collector);
            var sample = new RecordingSample();

            readings.Set(0, 0, 1500, 0, 0);
            Frame(clock, Phases());
            readings.Set(0, 0, 200, 0, 0);
            Frame(clock, Phases());
            readings.Set(0, 0, 700, 0, 0);
            Frame(clock, Phases());
            collector.Collect(sample);

            Assert.AreEqual(1000d, sample.Values["gc.allocated"]);
        }

        [Test]
        public void Allocations_NoCounter_NoMetric()
        {
            var collector = new GcAllocationsTelemetryCollector(new FakeMemoryReadings());
            collector.Start(_running);
            var sample = new RecordingSample();

            collector.Collect(sample);

            Assert.IsEmpty(sample.Values);
        }

        [Test]
        public void GpuQuery_OneInIntervalFrames_ResultInALaterFrame()
        {
            var query = new FakeGpuQuery { IsAvailable = true };
            var collector = new GpuQueryTelemetryCollector(query);
            collector.Start(_running);
            var clock = Clock(collector);
            var sample = new RecordingSample();

            Frame(clock, Phases());
            query.Result = 4d;

            for (var i = 1; i < GpuQueryTelemetryCollector.IntervalFrames * 2; i++)
                Frame(clock, Phases());

            collector.Collect(sample);

            Assert.AreEqual(2, query.Begins);
            Assert.AreEqual(query.Begins, query.Ends);
            Assert.AreEqual(4d, sample.Values["frame.gpu.query"]);
        }

        [Test]
        public void GpuQuery_DisjointOrUnavailable_NoMetric()
        {
            var query = new FakeGpuQuery { IsAvailable = true, Result = TelemetryGpuQueryResults.Lost };
            var collector = new GpuQueryTelemetryCollector(query);
            collector.Start(_running);
            var clock = Clock(collector);
            var sample = new RecordingSample();

            for (var i = 0; i < 5; i++)
                Frame(clock, Phases());

            collector.Collect(sample);
            Assert.IsEmpty(sample.Values);

            var absent = new FakeGpuQuery();
            var absentCollector = new GpuQueryTelemetryCollector(absent);
            absentCollector.Start(_running);
            Frame(Clock(absentCollector), Phases());
            Assert.AreEqual(0, absent.Begins);
        }

        [Test]
        public void GpuQuery_SessionEndsWithAnOpenQuery_Dropped()
        {
            var query = new FakeGpuQuery { IsAvailable = true };
            var collector = new GpuQueryTelemetryCollector(query);
            collector.Start(_running);

            collector.OnFrameStarted();
            _running.Terminate();

            Assert.AreEqual(1, query.Drops);
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-frame.test.js");
            var info = new ProcessStartInfo("node", $"--test \"{script}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, output + errors.Result);
        }
    }
}
