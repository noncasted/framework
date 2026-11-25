using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    public class TelemetryBatchWriterTests
    {
        private static TelemetryFixture Open(int bufferedSamples = 300)
        {
            var fixture = new TelemetryFixture(bufferedSamples: bufferedSamples);
            fixture.Buffer.Open();
            return fixture;
        }

        [Test]
        public void WriteNext_Nothing_Null()
        {
            Assert.IsNull(Open().Writer.WriteNext());
        }

        [Test]
        public void WriteNext_EverythingOfTheBuffer_AndItLeavesTheBuffer()
        {
            var fixture = Open();
            fixture.Sample(1000, sample =>
            {
                sample.Gauge("frame.dt_avg", 16.7, "ms");
                sample.Gauge("swarm.count", 240);
            });
            fixture.Clock.Set(1200);
            fixture.Telemetry.Mark("stage.boss_start");
            fixture.Clock.Set(1500);
            fixture.Errors.Add(TelemetryErrorLevel.Exception, null, "Broken", "Game.Swarm:Update ()", 3);

            var batch = fixture.Writer.WriteNext();

            CollectionAssert.AreEqual(new[] { "frame.dt_avg", "swarm.count" }, TelemetryFixture.NamesOf(batch));
            Assert.AreEqual("ms", batch.Names[0].Unit);
            Assert.IsNull(batch.Names[1].Unit);

            var sampled = batch.Samples.Single();
            Assert.AreEqual(1000, sampled.Time);
            CollectionAssert.AreEqual(new[] { 16.7, 240d }, sampled.Values);
            Assert.IsFalse(sampled.IsHidden);

            var mark = batch.Marks.Single();
            Assert.AreEqual(1200, mark.Time);
            Assert.AreEqual("stage.boss_start", mark.Name);

            var error = batch.Errors.Single();
            Assert.AreEqual(1500, error.Time);
            Assert.AreEqual(TelemetryErrorLevel.Exception, error.Level);
            Assert.AreEqual("Broken", error.Message);
            Assert.AreEqual("Game.Swarm:Update ()", error.Stack);
            Assert.AreEqual(3, error.Repeat);

            Assert.IsNull(fixture.Writer.WriteNext());
        }

        // The records of the buffer are reused: a batch in flight must not change with the next sample.
        [Test]
        public void WriteNext_BatchIsASnapshot()
        {
            var fixture = Open(bufferedSamples: 1);
            fixture.Sample(1000, sample => sample.Gauge("a", 1));
            var batch = fixture.Writer.WriteNext();

            fixture.Sample(2000, sample => sample.Gauge("a", 2));
            fixture.Writer.WriteNext();

            Assert.AreEqual(1000, batch.Samples.Single().Time);
            Assert.AreEqual(1d, batch.Samples.Single().Values.Single());
        }

        [Test]
        public void WriteNext_NewNameOnTheGo_OnlyItInNames_ValuesBySlots()
        {
            var fixture = Open();
            fixture.Sample(1000, sample =>
            {
                sample.Gauge("a", 1);
                sample.Gauge("b", 2, "ms");
            });
            var first = fixture.Writer.WriteNext();

            fixture.Sample(2000, sample =>
            {
                sample.Gauge("b", 3, "ms");
                sample.Gauge("c", 4, "bytes");
            });
            var second = fixture.Writer.WriteNext();

            CollectionAssert.AreEqual(new[] { "a", "b" }, TelemetryFixture.NamesOf(first));
            CollectionAssert.AreEqual(new[] { "c" }, TelemetryFixture.NamesOf(second));
            Assert.AreEqual("bytes", second.Names[0].Unit);

            var values = second.Samples[0].Values;
            Assert.IsTrue(double.IsNaN(values[0]));
            Assert.AreEqual(3d, values[1]);
            Assert.AreEqual(4d, values[2]);
        }

        // NaN is "no value" for the server, another non-finite number is not in the contract.
        [Test]
        public void WriteNext_NonFiniteValues_AreNoValue()
        {
            var fixture = Open();
            fixture.Sample(1000, sample =>
            {
                sample.Gauge("a", double.NaN);
                sample.Gauge("b", double.PositiveInfinity);
                sample.Gauge("c", double.NegativeInfinity);
                sample.Gauge("d", 1);
            });

            var values = fixture.Writer.WriteNext().Samples.Single().Values;

            Assert.IsTrue(double.IsNaN(values[0]));
            Assert.IsTrue(double.IsNaN(values[1]));
            Assert.IsTrue(double.IsNaN(values[2]));
            Assert.AreEqual(1d, values[3]);
        }

        // More than one batch can carry: the rest goes in the next ones, in order, nothing lost.
        [Test]
        public void WriteNext_MoreThanOneBatch_SplitsUnderLimits()
        {
            const int samples = 1000;
            var fixture = Open(samples);

            for (var i = 0; i < samples; i++)
            {
                fixture.Sample(i, sample =>
                {
                    for (var name = 0; name < 100; name++)
                        sample.Gauge($"metric.name_{name}", 1.5 + name);
                });
            }

            var times = new List<long>();

            for (var batch = fixture.Writer.WriteNext(); batch != null; batch = fixture.Writer.WriteNext())
            {
                Assert.LessOrEqual(batch.Samples.Count, TelemetryLimits.MaxSamplesPerBatch);
                Assert.LessOrEqual(SizeOf(batch), TelemetryLimits.MaxBatchBytes);
                times.AddRange(batch.Samples.Select(sample => sample.Time));
            }

            CollectionAssert.AreEqual(Enumerable.Range(0, samples).Select(time => (long)time), times);
        }

        [Test]
        public void WriteNext_FullSizeErrors_UnderTheLimitInTheWorstEncoding()
        {
            var fixture = Open();
            var written = 0;

            for (var i = 0; i < TelemetryLimits.MaxErrorsPerBatch; i++)
            {
                fixture.Errors.Add(
                    TelemetryErrorLevel.Error, null,
                    new string('ю', TelemetryLimits.MaxMessageLength),
                    new string('ю', TelemetryLimits.MaxStackLength),
                    1);
            }

            for (var batch = fixture.Writer.WriteNext(); batch != null; batch = fixture.Writer.WriteNext())
            {
                Assert.LessOrEqual(SizeOf(batch), TelemetryLimits.MaxBatchBytes);
                written += batch.Errors.Count;
            }

            Assert.AreEqual(TelemetryLimits.MaxErrorsPerBatch, written);
        }

        [Test]
        public void WriteAll_TakesEverything()
        {
            var fixture = Open();
            fixture.Clock.Set(5);
            fixture.Telemetry.Mark("a");

            for (var i = 0; i < TelemetryLimits.MaxErrorsPerBatch; i++)
                fixture.Errors.Add(TelemetryErrorLevel.Error, null, new string('m', TelemetryLimits.MaxMessageLength), null, 1);

            var batches = fixture.Writer.WriteAll();

            Assert.AreEqual(1, batches.Sum(batch => batch.Marks.Count));
            Assert.AreEqual(TelemetryLimits.MaxErrorsPerBatch, batches.Sum(batch => batch.Errors.Count));
            Assert.IsEmpty(fixture.Writer.WriteAll());
        }

        [Test]
        public void Errors_LongTexts_CutToContractLimits()
        {
            var fixture = Open();

            fixture.Errors.Add(
                TelemetryErrorLevel.Error, null,
                new string('m', TelemetryLimits.MaxMessageLength + 10),
                new string('s', TelemetryLimits.MaxStackLength + 10),
                1);

            var error = fixture.Writer.WriteNext().Errors.Single();

            Assert.AreEqual(TelemetryLimits.MaxMessageLength, error.Message.Length);
            Assert.AreEqual(TelemetryLimits.MaxStackLength, error.Stack.Length);
        }

        [Test]
        public void Errors_NullStack_StaysNull()
        {
            var fixture = Open();

            fixture.Errors.Add(TelemetryErrorLevel.Error, null, "failed", null, 1);

            Assert.IsNull(fixture.Writer.WriteNext().Errors.Single().Stack);
        }

        // The worst case on the wire: 3 bytes of UTF-8 per character, a few bytes around each record.
        private static int SizeOf(TelemetryBatch batch)
        {
            const int record = 32;

            return batch.Names.Sum(name => record + name.Name.Length + (name.Unit?.Length ?? 0))
                   + batch.Samples.Sum(sample => record + sample.Values.Count * sizeof(double))
                   + batch.Marks.Sum(mark => record + mark.Name.Length)
                   + batch.Errors.Sum(error => record + 3 * (error.Message.Length + (error.Stack?.Length ?? 0)));
        }
    }
}
