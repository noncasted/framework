using System;
using System.Collections;
using System.Collections.Generic;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Global.Telemetry.Tests
{
    // The collectors on real frames, the real updater and the real sources of Unity. The batchmode has no graphics, so
    // what depends on rendering is only checked not to break.
    public class TelemetryCollectorsPlayModeTests
    {
        private static readonly string[] _renderNames =
        {
            "render.draw_calls", "render.setpass", "render.triangles", "render.vertices", "render.textures",
            "render.render_textures",
        };

        private readonly List<GameObject> _created = new List<GameObject>();
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

            foreach (var created in _created)
                UnityEngine.Object.Destroy(created);

            _created.Clear();
        }

        private Updater CreateUpdater()
        {
            var updater = new GameObject("updater").AddComponent<Updater>();
            _created.Add(updater.gameObject);
            return updater;
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Frames_RealUpdater_SampleHasAllMetrics()
        {
            var collector = new FramesTelemetryCollector(CreateUpdater());
            var sample = new RecordingSample();

            collector.Start(_lifetime);
            yield return Frames(10);
            collector.Collect(sample);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "frame.fps", "frame.dt.avg", "frame.dt.p50", "frame.dt.p95", "frame.dt.max", "frame.over33", "frame.over50",
                },
                sample.Values.Keys);
            Assert.Greater(sample.Values["frame.fps"], 0d);
            Assert.Greater(sample.Values["frame.dt.max"], 0d);
        }

        // The speed of the updater is the time scale of the game: the frames of a paused game are still frames.
        [UnityTest]
        public IEnumerator Frames_SpeedOfTheUpdaterIsZero_StillRealDeltas()
        {
            var updater = CreateUpdater();
            var collector = new FramesTelemetryCollector(updater);
            var sample = new RecordingSample();

            updater.SetSpeed(0f);
            collector.Start(_lifetime);
            yield return Frames(10);
            collector.Collect(sample);

            Assert.Greater(sample.Values["frame.dt.avg"], 0d);
        }

        // What the last session did not take is not in the first sample of the next one.
        [UnityTest]
        public IEnumerator Frames_NextSession_NothingOfThePrevious()
        {
            var collector = new FramesTelemetryCollector(CreateUpdater());
            var sample = new RecordingSample();
            var first = new Lifetime();

            collector.Start(first);
            yield return null;
            yield return null;
            first.Terminate();

            collector.Start(_lifetime);
            collector.Collect(sample);

            CollectionAssert.IsEmpty(sample.Values);
        }

        [UnityTest]
        public IEnumerator Frames_LifetimeEnded_NoMoreFrames()
        {
            var collector = new FramesTelemetryCollector(CreateUpdater());
            var sample = new RecordingSample();
            var running = _lifetime.Child();

            collector.Start(running);
            yield return Frames(3);
            collector.Collect(sample);
            sample.Clear();
            running.Terminate();
            yield return Frames(3);
            collector.Collect(sample);

            CollectionAssert.IsEmpty(sample.Values);
        }

        [UnityTest]
        public IEnumerator Stats_RealProfiler_UnknownIsNotOpened_KnownIsClosedWithTheLifetime()
        {
            var source = new ProfilerTelemetryStatSource();
            var running = _lifetime.Child();

            Assert.IsFalse(source.TryOpen(running, "Render", "No Such Counter", out _));
            Assert.IsTrue(source.TryOpen(running, "Memory", "Total Used Memory", out var stat));

            yield return Frames(3);

            Assert.Greater(stat.LastValue, 0L);
            Assert.IsTrue(((ProfilerTelemetryStat)stat).IsValid);

            running.Terminate();

            Assert.IsFalse(((ProfilerTelemetryStat)stat).IsValid);
        }

        [UnityTest]
        public IEnumerator Render_RealProfiler_DoesNotThrow_OnlyItsNames()
        {
            var collector = new RenderTelemetryCollector(new ProfilerTelemetryStatSource());
            var sample = new RecordingSample();

            collector.Start(_lifetime);
            yield return Frames(3);
            collector.Collect(sample);

            CollectionAssert.IsSubsetOf(sample.Values.Keys, _renderNames);
        }

        [UnityTest]
        public IEnumerator Timings_RealManager_DoesNotThrow_OnlyItsNames()
        {
            var collector = new FrameTimingsTelemetryCollector(CreateUpdater(), new UnityTelemetryFrameTimings());
            var sample = new RecordingSample();

            collector.Start(_lifetime);
            yield return Frames(FrameTimingsTelemetryCollector.ProbeFrames + 2);
            collector.Collect(sample);

            CollectionAssert.IsSubsetOf(sample.Values.Keys, new[] { "frame.cpu", "frame.cpu.main", "frame.gpu" });
        }

        [UnityTest]
        public IEnumerator Memory_RealUnity_SampleHasTheMemoryOfTheEditor()
        {
            var collector = new MemoryTelemetryCollector(new UnityTelemetryMemoryReadings());
            var sample = new RecordingSample();

            yield return null;
            collector.Collect(sample);

            foreach (var name in new[] { "memory.allocated", "memory.reserved", "memory.mono.used", "memory.mono.heap" })
                Assert.Greater(sample.Values[name], 0d, name);

            Assert.GreaterOrEqual(sample.Values["gc.collections"], 0d);
        }

        // A check of the check: the meter must see what it is there to see, or zero means nothing.
        [Test]
        public void Allocations_TheMeterSeesAnAllocation()
        {
            Assert.That(() => { GC.KeepAlive(new byte[64]); }, Is.AllocatingGCMemory());
        }

        [Test]
        public void Allocations_FramesPerFrame_None()
        {
            var collector = new FramesTelemetryCollector(new NoAddUpdater());

            collector.OnUpdate(0.016f);

            Assert.That(() =>
            {
                for (var i = 0; i < 1000; i++)
                    collector.OnUpdate(0.016f);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Allocations_WindowAddAndTake_None()
        {
            var window = new FrameTimeWindow();

            window.Add(0.016f);
            window.TryTake(out _);

            Assert.That(() =>
            {
                for (var i = 0; i < 100; i++)
                    window.Add(0.016f + i * 0.001f);

                window.TryTake(out _);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Allocations_TimingsPerFrame_None()
        {
            var collector = new FrameTimingsTelemetryCollector(new NoAddUpdater(), new UnityTelemetryFrameTimings());

            collector.Start(_lifetime);
            collector.OnLateUpdate(0.016f);

            Assert.That(() =>
            {
                for (var i = 0; i < 20; i++)
                    collector.OnLateUpdate(0.016f);
            }, Is.Not.AllocatingGCMemory());
        }

        private class NoAddUpdater : IUpdater
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
    }
}
