using System.Collections.Generic;
using System.Linq;
using Internal;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The collectors over scripted sources: what is available gets a name, what is not gets nothing and throws nothing.
    public class TelemetryCollectorsTests
    {
        private static readonly string[] _renderCounters =
        {
            "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Used Textures Count",
            "Render Textures Count",
        };

        [Test]
        public void Memory_AllAvailable_EveryMetricWithItsUnit()
        {
            var readings = new FakeMemoryReadings();
            var collector = new MemoryTelemetryCollector(readings);
            var sample = new RecordingSample();

            readings.Set(allocated: 1, reserved: 2, monoUsed: 3, monoHeap: 4, textures: 5);
            readings.SetTextureCount(7);
            collector.Collect(sample);

            Assert.AreEqual(1d, sample.Values["memory.allocated"]);
            Assert.AreEqual(2d, sample.Values["memory.reserved"]);
            Assert.AreEqual(3d, sample.Values["memory.mono.used"]);
            Assert.AreEqual(4d, sample.Values["memory.mono.heap"]);
            Assert.AreEqual(5d, sample.Values["memory.textures"]);
            Assert.AreEqual(7d, sample.Values["memory.texture_count"]);
            Assert.AreEqual("count", sample.Units["memory.texture_count"]);

            foreach (var name in sample.Values.Keys.Where(name => name.StartsWith("memory.") && name != "memory.texture_count"))
                Assert.AreEqual("bytes", sample.Units[name], name);
        }

        // In a release build a counter that does not work reads zero: no metric rather than a false zero.
        [Test]
        public void Memory_ZeroReading_NotRegistered_TheRestGoesOn()
        {
            var readings = new FakeMemoryReadings();
            var collector = new MemoryTelemetryCollector(readings);
            var sample = new RecordingSample();

            readings.Set(allocated: 0, reserved: 0, monoUsed: 3, monoHeap: 4, textures: 0);
            collector.Collect(sample);

            CollectionAssert.AreEquivalent(
                new[] { "memory.mono.used", "memory.mono.heap", "gc.collections" },
                sample.Values.Keys);
        }

        [Test]
        public void Memory_Collections_TheGrowthSinceTheLastSample()
        {
            var readings = new FakeMemoryReadings();
            readings.SetCollections(10);
            var collector = new MemoryTelemetryCollector(readings);
            var sample = new RecordingSample();

            collector.Collect(sample);
            Assert.AreEqual(0d, sample.Values["gc.collections"]);

            readings.SetCollections(13);
            collector.Collect(sample);
            Assert.AreEqual(3d, sample.Values["gc.collections"]);
            Assert.AreEqual("count", sample.Units["gc.collections"]);

            collector.Collect(sample);
            Assert.AreEqual(0d, sample.Values["gc.collections"]);
        }

        [Test]
        public void Render_AllCounters_EveryMetric()
        {
            var source = new FakeStatSource(_renderCounters);
            var collector = new RenderTelemetryCollector(source);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            source.SetAll(7);
            collector.Collect(sample);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "render.draw_calls", "render.setpass", "render.triangles", "render.vertices", "render.textures",
                    "render.render_textures",
                },
                sample.Values.Keys);
            Assert.IsTrue(sample.Values.Values.All(value => value == 7d));
            Assert.IsTrue(sample.Units.Values.All(unit => unit == "count"));
        }

        [Test]
        public void Render_CountersAsked_ByTheCategoryOfUnity()
        {
            var source = new FakeStatSource(_renderCounters);

            new RenderTelemetryCollector(source).Start(new Lifetime());

            CollectionAssert.AreEquivalent(_renderCounters, source.Opened);
            Assert.IsTrue(source.Categories.All(category => category == "Render"));
        }

        [Test]
        public void Render_NoCounter_NotRegistered_NoThrow_NoLog()
        {
            var source = new FakeStatSource(new[] { "Draw Calls Count" });
            var collector = new RenderTelemetryCollector(source);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            source.SetAll(5);
            collector.Collect(sample);

            CollectionAssert.AreEquivalent(new[] { "render.draw_calls" }, sample.Values.Keys);
        }

        [Test]
        public void Render_NoCountersAtAll_NoMetric()
        {
            var collector = new RenderTelemetryCollector(new FakeStatSource(new string[0]));
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            collector.Collect(sample);

            CollectionAssert.IsEmpty(sample.Values);
        }

        // An open recorder of a counter a release does not feed reads zero for ever. Once it has worked, zero is a value.
        [Test]
        public void Render_ZeroUntilTheFirstValue_ThenZeroIsAValue()
        {
            var source = new FakeStatSource(new[] { "Draw Calls Count" });
            var collector = new RenderTelemetryCollector(source);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            source.SetAll(0);
            collector.Collect(sample);
            CollectionAssert.IsEmpty(sample.Values);

            source.SetAll(9);
            collector.Collect(sample);
            Assert.AreEqual(9d, sample.Values["render.draw_calls"]);

            source.SetAll(0);
            collector.Collect(sample);
            Assert.AreEqual(0d, sample.Values["render.draw_calls"]);
        }

        [Test]
        public void Timings_ValuesCome_AveragesOfTheWindowInMs()
        {
            var timings = new FakeFrameTimings();
            var collector = new FrameTimingsTelemetryCollector(new RecordingUpdater(), timings);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            timings.Set(new TelemetryFrameTiming(10, 8, 4));
            collector.OnLateUpdate(0f);
            timings.Set(new TelemetryFrameTiming(20, 12, 6));
            collector.OnLateUpdate(0f);
            collector.Collect(sample);

            Assert.AreEqual(15d, sample.Values["frame.cpu"]);
            Assert.AreEqual(10d, sample.Values["frame.cpu.main"]);
            Assert.AreEqual(5d, sample.Values["frame.gpu"]);
            Assert.IsTrue(sample.Units.Values.All(unit => unit == "ms"));
        }

        [Test]
        public void Timings_NoGpu_OnlyCpuRegistered()
        {
            var timings = new FakeFrameTimings();
            var collector = new FrameTimingsTelemetryCollector(new RecordingUpdater(), timings);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            timings.Set(new TelemetryFrameTiming(10, 8, 0));
            collector.OnLateUpdate(0f);
            collector.Collect(sample);

            CollectionAssert.AreEquivalent(new[] { "frame.cpu", "frame.cpu.main" }, sample.Values.Keys);
        }

        // GPU timers answer late, so a gap does not pull the average down.
        [Test]
        public void Timings_SomeFramesWithoutGpu_AverageOfTheOnesWithIt()
        {
            var timings = new FakeFrameTimings();
            var collector = new FrameTimingsTelemetryCollector(new RecordingUpdater(), timings);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            timings.Set(new TelemetryFrameTiming(10, 8, 0));
            collector.OnLateUpdate(0f);
            timings.Set(new TelemetryFrameTiming(10, 8, 6));
            collector.OnLateUpdate(0f);
            collector.Collect(sample);

            Assert.AreEqual(6d, sample.Values["frame.gpu"]);
        }

        // Each session asks for the frames of its own: what the last one did not take is not in the first sample of the next.
        [Test]
        public void Timings_NextSession_NothingOfThePrevious_ProbedAgain()
        {
            var updater = new RecordingUpdater();
            var timings = new FakeFrameTimings();
            var collector = new FrameTimingsTelemetryCollector(updater, timings);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            timings.Set(new TelemetryFrameTiming(5, 5, 5));
            collector.OnLateUpdate(0f);

            collector.Start(new Lifetime());
            collector.Collect(sample);
            CollectionAssert.IsEmpty(sample.Values);

            timings.Clear();

            for (var i = 0; i < FrameTimingsTelemetryCollector.ProbeFrames; i++)
                collector.OnLateUpdate(0f);

            Assert.IsTrue(updater.LateLifetime.IsTerminated);

            collector.Start(new Lifetime());
            Assert.IsFalse(updater.LateLifetime.IsTerminated);
            timings.Set(new TelemetryFrameTiming(0, 0, 3));

            for (var i = 0; i < FrameTimingsTelemetryCollector.ProbeFrames * 2; i++)
                collector.OnLateUpdate(0f);

            Assert.IsFalse(updater.LateLifetime.IsTerminated);
        }

        [Test]
        public void Memory_NextSession_CollectionsCountFromItsStart()
        {
            var readings = new FakeMemoryReadings();
            readings.SetCollections(10);
            var collector = new MemoryTelemetryCollector(readings);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            readings.SetCollections(25);
            collector.Start(new Lifetime());
            collector.Collect(sample);

            Assert.AreEqual(0d, sample.Values["gc.collections"]);
        }

        [Test]
        public void FrameWindow_Clear_NothingLeft()
        {
            var window = new FrameTimeWindow();
            window.Add(0.016f);

            window.Clear();

            Assert.IsFalse(window.TryTake(out _));
        }

        [Test]
        public void TimingAverage_Clear_NothingLeft()
        {
            var average = new FrameTimingAverage();
            average.Add(5d);

            average.Clear();

            Assert.IsFalse(average.TryTake(out _));
        }

        [Test]
        public void Timings_NothingInTheFirstFrames_StopsListening_NoMetric()
        {
            var updater = new RecordingUpdater();
            var timings = new FakeFrameTimings();
            var collector = new FrameTimingsTelemetryCollector(updater, timings);
            var sample = new RecordingSample();

            collector.Start(new Lifetime());
            Assert.IsFalse(updater.LateLifetime.IsTerminated);

            for (var i = 0; i < FrameTimingsTelemetryCollector.ProbeFrames; i++)
                collector.OnLateUpdate(0f);

            collector.Collect(sample);

            Assert.IsTrue(updater.LateLifetime.IsTerminated);
            CollectionAssert.IsEmpty(sample.Values);
        }

        [Test]
        public void Timings_AValueInTheFirstFrames_KeepsListening()
        {
            var updater = new RecordingUpdater();
            var timings = new FakeFrameTimings();
            var collector = new FrameTimingsTelemetryCollector(updater, timings);

            collector.Start(new Lifetime());
            timings.Set(new TelemetryFrameTiming(0, 0, 3));
            collector.OnLateUpdate(0f);
            timings.Clear();

            for (var i = 0; i < FrameTimingsTelemetryCollector.ProbeFrames * 2; i++)
                collector.OnLateUpdate(0f);

            Assert.IsFalse(updater.LateLifetime.IsTerminated);
        }

        [Test]
        public void Timings_ScopeEnded_ListeningEnds()
        {
            var updater = new RecordingUpdater();
            var running = new Lifetime();

            new FrameTimingsTelemetryCollector(updater, new FakeFrameTimings()).Start(running);
            running.Terminate();

            Assert.IsTrue(updater.LateLifetime.IsTerminated);
        }

        [Test]
        public void Frames_ListensOnTheLifetimeOfTheRun()
        {
            var updater = new RecordingUpdater();
            var running = new Lifetime();

            new FramesTelemetryCollector(updater).Start(running);

            Assert.AreSame(running, updater.UpdateLifetime);
        }

        [Test]
        public void Frames_NoFramesInTheWindow_NoMetric()
        {
            var sample = new RecordingSample();

            new FramesTelemetryCollector(new RecordingUpdater()).Collect(sample);

            CollectionAssert.IsEmpty(sample.Values);
        }

        [Test]
        public void WebMemory_AllKnown_EveryMetricInBytes()
        {
            var sample = new RecordingSample();

            new WebMemoryTelemetryCollector(new FakeWebMemory(100, 1, 2, 3)).Collect(sample);

            Assert.AreEqual(100d, sample.Values["web.wasm.heap"]);
            Assert.AreEqual(1d, sample.Values["web.js.used"]);
            Assert.AreEqual(2d, sample.Values["web.js.total"]);
            Assert.AreEqual(3d, sample.Values["web.js.limit"]);
            Assert.IsTrue(sample.Units.Values.All(unit => unit == "bytes"));
        }

        // Safari and Firefox have no performance.memory.
        [Test]
        public void WebMemory_NoPerformanceMemory_OnlyTheHeap()
        {
            var sample = new RecordingSample();

            new WebMemoryTelemetryCollector(new FakeWebMemory(100, -1, -1, -1)).Collect(sample);

            CollectionAssert.AreEquivalent(new[] { "web.wasm.heap" }, sample.Values.Keys);
        }

        // The server takes only this form: a metric of another form would be lost.
        [Test]
        public void Names_AllFitTheContract()
        {
            var timings = new FakeFrameTimings();
            var source = new FakeStatSource(_renderCounters);
            var memory = new FakeMemoryReadings();
            var frames = new FramesTelemetryCollector(new RecordingUpdater());
            var timing = new FrameTimingsTelemetryCollector(new RecordingUpdater(), timings);
            var render = new RenderTelemetryCollector(source);
            var sample = new RecordingSample();

            frames.OnUpdate(0f);
            timing.Start(new Lifetime());
            timings.Set(new TelemetryFrameTiming(1, 1, 1));
            timing.OnLateUpdate(0f);
            render.Start(new Lifetime());
            source.SetAll(1);
            memory.Set(1, 1, 1, 1, 1);

            timing.Collect(sample);
            render.Collect(sample);
            frames.Collect(sample);
            new MemoryTelemetryCollector(memory).Collect(sample);
            new WebMemoryTelemetryCollector(new FakeWebMemory(1, 1, 1, 1)).Collect(sample);

            foreach (var name in sample.Values.Keys)
            {
                Assert.IsTrue(TelemetryNameGuard.IsValid(name, TelemetryLimits.MaxNameLength), name);
                Assert.IsTrue(TelemetryNameGuard.IsValid(sample.Units[name], TelemetryLimits.MaxUnitLength), sample.Units[name]);
            }
        }
    }

    public class FakeMemoryReadings : ITelemetryMemoryReadings
    {
        public long Allocated { get; private set; }
        public long Reserved { get; private set; }
        public long MonoUsed { get; private set; }
        public long MonoHeap { get; private set; }
        public long Textures { get; private set; }
        public int Collections { get; private set; }
        public int TextureCount { get; private set; }

        public void SetTextureCount(int count)
        {
            TextureCount = count;
        }

        public void Set(long allocated, long reserved, long monoUsed, long monoHeap, long textures)
        {
            Allocated = allocated;
            Reserved = reserved;
            MonoUsed = monoUsed;
            MonoHeap = monoHeap;
            Textures = textures;
        }

        public void SetCollections(int collections)
        {
            Collections = collections;
        }
    }

    public class FakeStat : ITelemetryStat
    {
        public long LastValue { get; private set; }

        public void Set(long value)
        {
            LastValue = value;
        }
    }

    public class FakeStatSource : ITelemetryStatSource
    {
        public FakeStatSource(IReadOnlyList<string> available)
        {
            _available = available;
        }

        private readonly IReadOnlyList<string> _available;
        private readonly List<FakeStat> _stats = new List<FakeStat>();

        public List<string> Opened { get; } = new List<string>();
        public List<string> Categories { get; } = new List<string>();

        public bool TryOpen(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryStat stat)
        {
            Categories.Add(category);
            stat = null;

            if (_available.Contains(name) == false)
                return false;

            var opened = new FakeStat();
            _stats.Add(opened);
            Opened.Add(name);
            stat = opened;
            return true;
        }

        public void SetAll(long value)
        {
            foreach (var stat in _stats)
                stat.Set(value);
        }
    }

    public class FakeFrameTimings : ITelemetryFrameTimings
    {
        private TelemetryFrameTiming _timing;

        public void Set(TelemetryFrameTiming timing)
        {
            _timing = timing;
        }

        public void Clear()
        {
            _timing = default;
        }

        public TelemetryFrameTiming Read()
        {
            return _timing;
        }
    }

    public class FakeWebMemory : ITelemetryWebMemory
    {
        public FakeWebMemory(double heap, double jsUsed, double jsTotal, double jsLimit)
        {
            Heap = heap;
            JsUsed = jsUsed;
            JsTotal = jsTotal;
            JsLimit = jsLimit;
        }

        public double Heap { get; }
        public double JsUsed { get; }
        public double JsTotal { get; }
        public double JsLimit { get; }
    }

    // Remembers what was subscribed: the frames in these tests are given by hand.
    public class RecordingUpdater : IUpdater
    {
        public IReadOnlyLifetime UpdateLifetime { get; private set; }
        public IReadOnlyLifetime LateLifetime { get; private set; }

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
            UpdateLifetime = l;
        }

        public void Add(IReadOnlyLifetime l, ILateUpdatable updatable)
        {
            LateLifetime = l;
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
    }
}
