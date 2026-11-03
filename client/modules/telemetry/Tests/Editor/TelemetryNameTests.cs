using System;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    public class TelemetryNameTests
    {
        private static readonly string[] _invalidNames =
        {
            null,
            "",
            "Upper",
            "with space",
            "dash-name",
            "кириллица",
            "user@mail.com",
        };

        [Test]
        public void Rules_ValidNames_Pass()
        {
            Assert.IsTrue(TelemetryNameGuard.IsValid("frame.dt_avg", TelemetryLimits.MaxNameLength));
            Assert.IsTrue(TelemetryNameGuard.IsValid("a0._", TelemetryLimits.MaxNameLength));
            Assert.IsTrue(TelemetryNameGuard.IsValid(new string('a', TelemetryLimits.MaxNameLength), TelemetryLimits.MaxNameLength));
            Assert.IsFalse(TelemetryNameGuard.IsValid(new string('a', TelemetryLimits.MaxNameLength + 1), TelemetryLimits.MaxNameLength));
        }

        [Test]
        public void Mark_InvalidName_DevelopmentThrows([ValueSource(nameof(_invalidNames))] string name)
        {
            var fixture = new TelemetryFixture(strictNames: true);

            Assert.Throws<ArgumentException>(() => fixture.Telemetry.Mark(name));
        }

        [Test]
        public void Mark_InvalidName_ReleaseDropsIt([ValueSource(nameof(_invalidNames))] string name)
        {
            var fixture = new TelemetryFixture(strictNames: false);
            fixture.Buffer.Open();

            fixture.Telemetry.Mark(name);
            fixture.Clock.Set(10);
            fixture.Telemetry.Mark("valid");

            CollectionAssert.AreEqual(new[] { "valid" }, fixture.Writer.WriteNext().Marks.Select(mark => mark.Name));
        }

        [Test]
        public void Gauge_InvalidName_DevelopmentThrows()
        {
            var fixture = new TelemetryFixture(strictNames: true);

            Assert.Throws<ArgumentException>(() => fixture.Names.TryGetIndex("Bad Name", null, out _));
        }

        [Test]
        public void Gauge_InvalidName_ReleaseKeepsTheOthersAndTheirIndices()
        {
            var fixture = new TelemetryFixture(strictNames: false);
            fixture.Buffer.Open();

            fixture.Sample(1000, sample =>
            {
                sample.Gauge("Bad Name", 1);
                sample.Gauge("good", 2);
            });

            var batch = fixture.Writer.WriteNext();

            CollectionAssert.AreEqual(new[] { "good" }, TelemetryFixture.NamesOf(batch));
            Assert.AreEqual(2d, batch.Samples[0].Values[0]);
        }

        [Test]
        public void Gauge_InvalidUnit_ReleaseDropsTheValue()
        {
            var fixture = new TelemetryFixture(strictNames: false);
            fixture.Buffer.Open();

            fixture.Sample(1000, sample => sample.Gauge("a", 1, "Bad Unit"));

            Assert.IsNull(fixture.Writer.WriteNext());
        }

        [Test]
        public void Gauge_InvalidUnit_DevelopmentThrows()
        {
            var fixture = new TelemetryFixture(strictNames: true);

            Assert.Throws<ArgumentException>(() => fixture.Names.TryGetIndex("a", "Bad Unit", out _));
        }

        [Test]
        public void Gauge_OtherUnitOfKnownName_DevelopmentThrows()
        {
            var fixture = new TelemetryFixture(strictNames: true);

            fixture.Names.TryGetIndex("a", "ms", out _);

            Assert.Throws<ArgumentException>(() => fixture.Names.TryGetIndex("a", "bytes", out _));
        }

        // The table of a session starts over for the next one: the first name takes the first slot again.
        [Test]
        public void Clear_NamesTakeTheSlotsAgain_AndAreNewAgain()
        {
            var fixture = new TelemetryFixture();
            fixture.Names.TryGetIndex("a", null, out _);
            fixture.Names.TryGetIndex("b", null, out _);
            fixture.Names.MarkAllWritten();

            fixture.Names.Clear();

            Assert.AreEqual(0, fixture.Names.Count);
            Assert.AreEqual(0, fixture.Names.Written);
            Assert.IsTrue(fixture.Names.TryGetIndex("b", "ms", out var index));
            Assert.AreEqual(0, index);
        }

        // The server rejects a whole batch over the limit of the session.
        [Test]
        public void Names_OverSessionLimit_DevelopmentThrows()
        {
            var fixture = new TelemetryFixture(strictNames: true);

            for (var i = 0; i < TelemetryLimits.MaxNamesPerSession; i++)
                Assert.IsTrue(fixture.Names.TryGetIndex($"a{i}", null, out _));

            Assert.Throws<ArgumentException>(() => fixture.Names.TryGetIndex("over", null, out _));
        }

        [Test]
        public void Names_OverSessionLimit_ReleaseDropsNewOnes()
        {
            var fixture = new TelemetryFixture(strictNames: false);

            for (var i = 0; i < TelemetryLimits.MaxNamesPerSession; i++)
                Assert.IsTrue(fixture.Names.TryGetIndex($"a{i}", null, out _));

            Assert.IsFalse(fixture.Names.TryGetIndex("over", null, out _));
            Assert.IsTrue(fixture.Names.TryGetIndex("a0", null, out _));
        }
    }
}
