using System.Diagnostics;
using System.IO;
using Internal;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The hidden tab without the browser: the events come from a fake page. TelemetryPage.jslib is on node below.
    public class TelemetryPageTests
    {
        private static void SampleOne(TelemetryFixture fixture, long time)
        {
            fixture.Sample(time, sample => sample.Gauge("a", 1));
        }

        [Test]
        public void Sample_TabHiddenForAPartOfTheSecond_OnlyThatSampleIsHidden()
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();
            fixture.Buffer.Open();
            fixture.Visibility.Listen(lifetime);

            SampleOne(fixture, 1000);
            fixture.Page.Hide();
            fixture.Page.Show();
            SampleOne(fixture, 2000);
            SampleOne(fixture, 3000);

            var samples = fixture.Writer.WriteNext().Samples;
            Assert.IsFalse(samples[0].IsHidden);
            Assert.IsTrue(samples[1].IsHidden);
            Assert.IsFalse(samples[2].IsHidden);

            lifetime.Terminate();
        }

        [Test]
        public void Sample_TabStaysHidden_EverySampleIsHidden()
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();
            fixture.Buffer.Open();
            fixture.Visibility.Listen(lifetime);

            fixture.Page.Hide();
            SampleOne(fixture, 1000);
            SampleOne(fixture, 2000);
            fixture.Page.Show();
            SampleOne(fixture, 3000);
            SampleOne(fixture, 4000);

            var samples = fixture.Writer.WriteNext().Samples;
            Assert.IsTrue(samples[0].IsHidden);
            Assert.IsTrue(samples[1].IsHidden);
            Assert.IsFalse(samples[2].IsHidden, "Shown before this sample, not hidden during it");
            Assert.IsFalse(samples[3].IsHidden);

            lifetime.Terminate();
        }

        // A pass without any value leaves no sample, and the hidden time of that pass does not stick to the next one.
        [Test]
        public void Sample_NoValuesWhileHidden_NextSampleIsNotHidden()
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();
            fixture.Buffer.Open();
            fixture.Visibility.Listen(lifetime);

            fixture.Page.Hide();
            fixture.Page.Show();
            fixture.Sample(1000, _ => { });
            SampleOne(fixture, 2000);

            Assert.IsFalse(fixture.Writer.WriteNext().Samples[0].IsHidden);

            lifetime.Terminate();
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-page.test.js");
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
