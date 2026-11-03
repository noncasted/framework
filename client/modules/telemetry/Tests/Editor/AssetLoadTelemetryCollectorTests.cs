using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The loads of assets in a session: the start, the end with the duration, a failure, a slow one and what is unfinished at the
    // end of the session, with the key of the code in the detail.
    public class AssetLoadTelemetryCollectorTests
    {
        private readonly List<AssetLoad> _leftover = new();

        private sealed class Gauges : ITelemetrySample
        {
            public readonly Dictionary<string, double> Values = new();

            public void Gauge(string name, double value, string unit = null)
            {
                Values[name] = value;
            }
        }

        private static (TelemetryFixture Fixture, AssetLoadTelemetryCollector Collector, Lifetime Session) Open(float slowLoadSeconds = 5f)
        {
            var fixture = new TelemetryFixture();
            var options = new ClientTelemetryOptions(slowLoadSeconds: slowLoadSeconds, enabledInEditor: true);
            var collector = new AssetLoadTelemetryCollector(options, fixture.Buffer, fixture.Clock);
            var session = new Lifetime();

            fixture.Buffer.Open();
            collector.Start(session);

            return (fixture, collector, session);
        }

        private static string[] Marks(TelemetryFixture fixture)
        {
            return fixture.Writer.WriteAll().SelectMany(batch => batch.Marks).Select(mark => $"{mark.Name} {mark.Detail}").ToArray();
        }

        private AssetLoad Begin(string key)
        {
            var load = AssetLoads.Begin(key);
            _leftover.Add(load);
            return load;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var load in _leftover.Where(load => load.IsOver == false))
                load.Finish();

            _leftover.Clear();
        }

        [Test]
        public void ALoad_IsAStartAndAnEnd_WithTheKeyAndTheDuration()
        {
            var (fixture, _, session) = Open();

            fixture.Clock.Set(100);
            var load = Begin("Group: MusicRunAudio");
            fixture.Clock.Set(300);
            load.Finish();
            session.Terminate();

            var marks = fixture.Writer.WriteAll().SelectMany(batch => batch.Marks).ToArray();

            CollectionAssert.AreEqual(new[] { "load.start", "load.end" }, marks.Select(mark => mark.Name).ToArray());
            Assert.AreEqual("Group: MusicRunAudio", marks[0].Detail);
            Assert.AreEqual(100, marks[0].Time);
            Assert.IsNull(marks[0].DurationMs);
            Assert.AreEqual(300, marks[1].Time);
            Assert.GreaterOrEqual(marks[1].DurationMs.Value, 0L);
        }

        [Test]
        public void AFailedLoad_IsMarkedWithTheTypeOfItsError()
        {
            var (fixture, _, session) = Open();

            Begin("Group: MusicRunAudio").Fail(new InvalidOperationException("no bundle"));
            session.Terminate();

            CollectionAssert.AreEqual(
                new[] { "load.start Group: MusicRunAudio", "load.failed Group: MusicRunAudio (InvalidOperationException)" },
                Marks(fixture));
        }

        [Test]
        public void ALoadStillGoingAtTheEndOfTheSession_IsUnfinished()
        {
            var (fixture, _, session) = Open();

            Begin("Scene");
            session.Terminate();

            CollectionAssert.AreEqual(new[] { "load.start Scene", "load.unfinished Scene" }, Marks(fixture));
        }

        [Test]
        public void ALoadBegunBeforeTheSession_EndsInIt_WithoutAStart()
        {
            var load = Begin("Group: MenuAudio");
            var (fixture, _, session) = Open();

            load.Finish();
            session.Terminate();

            CollectionAssert.AreEqual(new[] { "load.end Group: MenuAudio" }, Marks(fixture));
        }

        [Test]
        public void ASlowLoad_IsMarkedOnce()
        {
            var (fixture, collector, session) = Open(slowLoadSeconds: 0f);
            var gauges = new Gauges();

            Begin("Group: MusicRunAudio");
            collector.Collect(gauges);
            collector.Collect(gauges);
            session.Terminate();

            CollectionAssert.IsEmpty(gauges.Values);
            CollectionAssert.AreEqual(
                new[] { "load.start Group: MusicRunAudio", "load.slow Group: MusicRunAudio", "load.unfinished Group: MusicRunAudio" },
                Marks(fixture));
        }

        [Test]
        public void AfterTheSession_NothingIsMarked()
        {
            var (fixture, _, session) = Open();

            session.Terminate();
            Begin("Group: A").Finish();

            CollectionAssert.IsEmpty(Marks(fixture));
        }
    }
}
