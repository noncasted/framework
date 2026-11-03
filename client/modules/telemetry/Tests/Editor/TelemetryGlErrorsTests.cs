using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The errors of WebGL nobody asks for in a release build: taken once a sample, counted, the first ones marked, every kind an error.
    public class TelemetryGlErrorsTests
    {
        private const int OutOfMemory = 0x0505;
        private const int InvalidOperation = 0x0502;

        private FakeTelemetryGlErrors _errors;
        private TelemetryFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _errors = new FakeTelemetryGlErrors();
            _fixture = new TelemetryFixture(catchErrors: true, glErrors: _errors);
        }

        private static IEnumerable<string> MarksOf(IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Marks).Select(mark => mark.Name);
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
        public void ErrorsOfASample_AreCounted_MarkedByCode_AndSentAsErrors()
        {
            var running = _fixture.Collection.Begin();

            _fixture.Clock.Set(2000);
            _errors.Raise(OutOfMemory, InvalidOperation);
            _fixture.Sampler.Sample();
            _fixture.Clock.Set(3000);
            _fixture.Sampler.Sample();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { 2d, 0d }, ValuesOf(batches, "web.gl.errors"));
            CollectionAssert.AreEqual(new[] { "webgl.error.out_of_memory", "webgl.error.invalid_operation" }, MarksOf(batches));
            Assert.AreEqual(2000, batches.SelectMany(batch => batch.Marks).First().Time);

            var messages = batches.SelectMany(batch => batch.Errors).Select(error => error.Message).ToArray();
            CollectionAssert.AreEquivalent(new[] { "WebGL error OUT_OF_MEMORY", "WebGL error INVALID_OPERATION" }, messages);
        }

        [Test]
        public void TheTimeOfTheTake_IsSampled()
        {
            var running = _fixture.Collection.Begin();

            _errors.CostOfTheNextTake(3.5);
            _fixture.Sampler.Sample();

            CollectionAssert.AreEqual(new[] { 3.5 }, ValuesOf(_fixture.Collection.End(running), "web.gl.errors_ms"));
        }

        [Test]
        public void AnUnknownCode_IsOther()
        {
            var running = _fixture.Collection.Begin();

            _errors.Raise(0x1234);
            _fixture.Sampler.Sample();

            CollectionAssert.AreEqual(new[] { "webgl.error.other" }, MarksOf(_fixture.Collection.End(running)));
        }

        [Test]
        public void ManyErrors_OnlyTheFirstMarks_TheCountGoesOn()
        {
            var running = _fixture.Collection.Begin();

            for (var i = 0; i < GlErrorsTelemetryCollector.MaxMarks + 4; i++)
            {
                _errors.Raise(InvalidOperation);
                _fixture.Sampler.Sample();
            }

            var batches = _fixture.Collection.End(running);

            Assert.AreEqual(GlErrorsTelemetryCollector.MaxMarks, MarksOf(batches).Count());
            Assert.AreEqual(GlErrorsTelemetryCollector.MaxMarks + 4, ValuesOf(batches, "web.gl.errors").Sum());
        }

        // The page keeps one flag per code until it is taken: the collector asks for a bounded number of them at a time.
        [Test]
        public void TheTake_IsBounded()
        {
            var running = _fixture.Collection.Begin();

            _fixture.Sampler.Sample();

            Assert.AreEqual(GlErrorsTelemetryCollector.MaxCodesPerSample, _errors.LastMax);
            _fixture.Collection.End(running);
        }

        [Test]
        public void TheNames_AreValid()
        {
            foreach (var code in new[] { 0x0500, 0x0501, 0x0502, 0x0505, 0x0506, 0x9242, 1 })
            {
                var running = _fixture.Collection.Begin();
                _errors.Raise(code);
                _fixture.Sampler.Sample();

                foreach (var mark in MarksOf(_fixture.Collection.End(running)))
                    Assert.IsTrue(TelemetryNameGuard.IsValid(mark, TelemetryLimits.MaxNameLength), mark);
            }
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-gl-errors.test.js");
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
