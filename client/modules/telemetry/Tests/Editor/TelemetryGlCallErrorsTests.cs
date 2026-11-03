using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The errors of WebGL with the call: the ones emscripten records itself, apart from getError, and the check of every call.
    public class TelemetryGlCallErrorsTests
    {
        private const int InvalidEnum = 0x0500;
        private const int InvalidOperation = 0x0502;

        private FakeTelemetryGlCallErrors _errors;
        private TelemetryFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _errors = new FakeTelemetryGlCallErrors();
            _fixture = new TelemetryFixture(catchErrors: true, glCallErrors: _errors);
        }

        private static IReadOnlyList<TelemetryBatchMark> MarksOf(IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Marks).ToArray();
        }

        private static bool HasMetric(IReadOnlyList<TelemetryBatch> batches, string metric)
        {
            return batches.Any(batch => batch.Names.Any(name => name.Name == metric));
        }

        private static IReadOnlyList<double> ValuesOf(IReadOnlyList<TelemetryBatch> batches, string metric)
        {
            var values = new List<double>();

            foreach (var batch in batches)
            {
                for (var slot = 0; slot < batch.Names.Count; slot++)
                {
                    if (batch.Names[slot].Name == metric)
                        values.AddRange(batch.Samples.Select(sample => sample.Values[slot]));
                }
            }

            return values;
        }

        [Test]
        public void TheErrorsOfEmscripten_AreCountedApart_WithTheCall()
        {
            var running = _fixture.Collection.Begin();

            _fixture.Clock.Set(1500);
            _errors.Record(InvalidEnum, "glGetIntegerv", 1234.5);
            _errors.RecordAgain();
            _fixture.Sampler.Sample();
            _fixture.Sampler.Sample();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { 2d, 0d }, ValuesOf(batches, "web.gl.emscripten_errors"));
            Assert.IsFalse(HasMetric(batches, "web.gl.check_errors"), "the check is off");

            var mark = MarksOf(batches).Single();
            Assert.AreEqual("webgl.emscripten.invalid_enum", mark.Name);
            Assert.AreEqual("glGetIntegerv", mark.Detail);
            Assert.AreEqual(1500, mark.Time);

            var error = batches.SelectMany(batch => batch.Errors).Single();
            Assert.AreEqual("WebGL error INVALID_ENUM in glGetIntegerv (emscripten)", error.Message);
            StringAssert.Contains("1235 ms since the start of the page", error.Stack);
        }

        [Test]
        public void TheCheck_GivesTheCall_ItsArguments_AndTheFrame()
        {
            var running = _fixture.Collection.Begin();

            _errors.Check();
            _errors.Fail(InvalidOperation, "bindTexture", "0x1234,null", 87, 1100);
            _fixture.Sampler.Sample();
            _fixture.Sampler.Sample();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { 1d, 0d }, ValuesOf(batches, "web.gl.check_errors"));

            var mark = MarksOf(batches).Single();
            Assert.AreEqual("webgl.check.invalid_operation", mark.Name);
            Assert.AreEqual("bindTexture(0x1234,null)", mark.Detail);

            var error = batches.SelectMany(batch => batch.Errors).Single();
            Assert.AreEqual("WebGL error INVALID_OPERATION in bindTexture", error.Message);
            StringAssert.Contains("frame 87, 1100 ms", error.Stack);
        }

        [Test]
        public void ManyCalls_OnlyTheFirstMark()
        {
            var running = _fixture.Collection.Begin();

            for (var i = 0; i < GlCallErrorsTelemetryCollector.MaxMarks + 4; i++)
            {
                _errors.Record(InvalidOperation, "gl" + i, i);
                _fixture.Sampler.Sample();
            }

            Assert.AreEqual(GlCallErrorsTelemetryCollector.MaxMarks, MarksOf(_fixture.Collection.End(running)).Count);
        }

        [Test]
        public void TheNames_AreValid()
        {
            foreach (var code in new[] { 0x0500, 0x0501, 0x0502, 0x0505, 0x0506, 0x9242, 1 })
            {
                var running = _fixture.Collection.Begin();
                _errors.Check();
                _errors.Record(code, "glX", 0);
                _errors.Fail(code, "x", string.Empty, 0, 0);
                _fixture.Sampler.Sample();

                foreach (var mark in MarksOf(_fixture.Collection.End(running)))
                    Assert.IsTrue(TelemetryNameGuard.IsValid(mark.Name, TelemetryLimits.MaxNameLength), mark.Name);
            }
        }

        [Test]
        public void TheLinesOfThePage_AreParsed()
        {
            var details = new List<TelemetryGlCallError>();

            TelemetryGlCallErrorLines.Parse("emscripten\t1280\tglGetIntegerv\t\t-1\t812.4\ncheck\t1282\tbindTexture\t0x1234,null\t31\t1100.0", details);

            Assert.AreEqual(2, details.Count);
            Assert.IsFalse(details[0].IsChecked);
            Assert.AreEqual(1280, details[0].Code);
            Assert.AreEqual("glGetIntegerv", details[0].Call);
            Assert.AreEqual(812.4, details[0].PageMs, 1e-9);
            Assert.IsTrue(details[1].IsChecked);
            Assert.AreEqual("0x1234,null", details[1].Arguments);
            Assert.AreEqual(31, details[1].Frame);
        }

        [Test]
        public void ABrokenLine_Throws()
        {
            Assert.Throws<System.FormatException>(() => TelemetryGlCallErrorLines.Parse("check\t1282", new List<TelemetryGlCallError>()));
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-gl-call-errors.test.js");
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
