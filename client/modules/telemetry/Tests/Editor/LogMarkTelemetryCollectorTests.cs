using System.Linq;
using System.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The records of the logger at the mark level of their category as marks of the session: the name by the level, the
    // short name of the category and the template in the detail, the time of the call.
    public class LogMarkTelemetryCollectorTests
    {
        private const string _category = "Global.Telemetry.Tests.LogMarkTelemetryCollectorTests.Owner";

        [TearDown]
        public void TearDown()
        {
            Logs.Configure(new LogOptions());
        }

        private static (TelemetryFixture Fixture, LogMarkTelemetryCollector Collector, Lifetime Session) Open(LogLevel mark)
        {
            Logs.Configure(new LogOptions(LogLevel.Warning, LogLevel.Warning, LogLevel.None,
                new LogRule(_category, LogLevel.Warning, LogLevel.Warning, mark)));

            var fixture = new TelemetryFixture();
            var collector = new LogMarkTelemetryCollector(fixture.Buffer, fixture.Clock);
            var session = new Lifetime();

            fixture.Buffer.Open();
            collector.Start(session);

            return (fixture, collector, session);
        }

        private static string[] Marks(TelemetryFixture fixture)
        {
            return fixture.Writer.WriteAll().SelectMany(batch => batch.Marks).Select(mark => $"{mark.Time} {mark.Name} {mark.Detail}").ToArray();
        }

        [Test]
        public void ARecordAtTheMarkLevel_IsAMarkWithTheTemplate()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, collector, _) = Open(LogLevel.Info);

            fixture.Clock.Set(120);
            Logs.Of<Owner>().Info("Joined the room {Code}", 42);
            fixture.Clock.Set(900);
            collector.Collect(null);

            CollectionAssert.AreEqual(new[] { "120 log.info Owner: Joined the room {Code}" }, Marks(fixture));
        }

        [Test]
        public void TheNameIsTheLevel()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, _, session) = Open(LogLevel.Debug);

            var log = Logs.Of<Owner>();
            log.Debug("d");
            log.Warning("w");
            log.Error("e");
            session.Terminate();

            LogAssert.Expect(UnityEngine.LogType.Warning, "[Owner] w");
            CollectionAssert.AreEqual(new[] { "0 log.debug Owner: d", "0 log.warning Owner: w", "0 log.error Owner: e" }, Marks(fixture));
        }

        [Test]
        public void BelowTheMarkLevel_NoMark()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, collector, _) = Open(LogLevel.Warning);

            Logs.Of<Owner>().Info("Joined");
            collector.Collect(null);

            Assert.IsEmpty(Marks(fixture));
        }

        [Test]
        public void WithoutAMarkLevel_NoMark()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, collector, _) = Open(LogLevel.None);

            Logs.Of<Owner>().Error("Lost");
            collector.Collect(null);

            Assert.IsEmpty(Marks(fixture));
        }

        [Test]
        public void TheEndOfTheSession_TakesThePendingMarks()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, _, session) = Open(LogLevel.Info);

            Logs.Of<Owner>().Info("Left");
            session.Terminate();

            CollectionAssert.AreEqual(new[] { "0 log.info Owner: Left" }, Marks(fixture));
        }

        [Test]
        public void AfterTheSession_NoMark()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, collector, session) = Open(LogLevel.Info);

            session.Terminate();
            Logs.Of<Owner>().Info("Late");
            collector.Collect(null);

            Assert.IsEmpty(Marks(fixture));
        }

        [Test]
        public void AFloodBetweenTwoSamples_KeepsItsFirstMarks()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, collector, _) = Open(LogLevel.Info);

            for (var i = 0; i < LogMarkTelemetryCollector.MaxPending + 10; i++)
                Logs.Of<Owner>().Info("Tick {Index}", i);

            collector.Collect(null);

            Assert.AreEqual(LogMarkTelemetryCollector.MaxPending, Marks(fixture).Length);
        }

        [Test]
        public void ARecordOfAnotherThread_ReachesTheBufferWithTheNextSample()
        {
            LogAssert.ignoreFailingMessages = true;
            var (fixture, collector, _) = Open(LogLevel.Info);

            Task.Run(() => Logs.Of<Owner>().Info("From a thread")).Wait();
            collector.Collect(null);

            CollectionAssert.AreEqual(new[] { "0 log.info Owner: From a thread" }, Marks(fixture));
        }

        private class Owner
        {
        }
    }
}
