using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    public class TelemetryBufferTests
    {
        [Test]
        public void Samples_Overflow_LosesOldestWithoutThrowing()
        {
            var fixture = new TelemetryFixture(bufferedSamples: 3);
            fixture.Buffer.Open();

            for (var time = 1; time <= 5; time++)
            {
                var value = time;
                fixture.Sample(time, sample => sample.Gauge("a", value));
            }

            var samples = fixture.Writer.WriteNext().Samples;

            CollectionAssert.AreEqual(new long[] { 3, 4, 5 }, samples.Select(sample => sample.Time));
            CollectionAssert.AreEqual(new double[] { 3, 4, 5 }, samples.Select(sample => sample.Values[0]));
        }

        [Test]
        public void Marks_Overflow_LosesOldestUnderBatchLimit()
        {
            var fixture = new TelemetryFixture();
            fixture.Buffer.Open();

            for (var time = 0; time < TelemetryLimits.MaxMarksPerBatch + 5; time++)
            {
                fixture.Clock.Set(time);
                fixture.Telemetry.Mark("a");
            }

            var marks = fixture.Writer.WriteNext().Marks;

            Assert.AreEqual(TelemetryLimits.MaxMarksPerBatch, marks.Count);
            Assert.AreEqual(5, marks[0].Time);
        }

        [Test]
        public void Errors_Overflow_LosesOldestUnderBatchLimit()
        {
            var fixture = new TelemetryFixture();
            fixture.Buffer.Open();

            for (var time = 0; time < TelemetryLimits.MaxErrorsPerBatch + 5; time++)
            {
                fixture.Clock.Set(time);
                fixture.Errors.Add(TelemetryErrorLevel.Error, null, "failed", null, 1);
            }

            var errors = fixture.Writer.WriteNext().Errors;

            Assert.AreEqual(TelemetryLimits.MaxErrorsPerBatch, errors.Count);
            Assert.AreEqual(5, errors[0].Time);
        }

        [Test]
        public void Close_DropsAndIgnoresEverything()
        {
            var fixture = new TelemetryFixture();
            fixture.Buffer.Open();
            fixture.Sample(1000, sample => sample.Gauge("a", 1));
            fixture.Writer.WriteNext();
            fixture.Sample(2000, sample => sample.Gauge("a", 2));

            fixture.Buffer.Close();
            fixture.Sample(3000, sample => sample.Gauge("a", 3));
            fixture.Telemetry.Mark("a");
            fixture.Errors.Add(TelemetryErrorLevel.Error, null, "failed", null, 1);

            Assert.IsNull(fixture.Writer.WriteNext());
        }

        [Test]
        public void NotOpen_TakesNothing()
        {
            var fixture = new TelemetryFixture();

            fixture.Sample(1000, sample => sample.Gauge("a", 1));
            fixture.Telemetry.Mark("a");
            fixture.Errors.Add(TelemetryErrorLevel.Error, null, "failed", null, 1);

            Assert.AreEqual(0, fixture.Buffer.Samples.Count);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
            Assert.AreEqual(0, fixture.Buffer.Errors.Count);
        }

        [Test]
        public void Open_StartsEmpty_AfterClose_TakesAgain()
        {
            var fixture = new TelemetryFixture();
            fixture.Buffer.Open();
            fixture.Telemetry.Mark("a");
            fixture.Buffer.Close();

            fixture.Buffer.Open();
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);

            fixture.Telemetry.Mark("b");
            Assert.AreEqual("b", fixture.Writer.WriteNext().Marks.Single().Name);
        }
    }
}
