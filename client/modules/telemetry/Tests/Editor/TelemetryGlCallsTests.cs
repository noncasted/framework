using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The calls of WebGL counted on the page: compilations and links of shaders with their time, uploads, the slow calls as marks.
    public class TelemetryGlCallsTests
    {
        private FakeTelemetryGlCalls _calls;
        private TelemetryFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _calls = new FakeTelemetryGlCalls();
            _fixture = new TelemetryFixture(glCalls: _calls);
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

        private static IReadOnlyList<TelemetryBatchMark> MarksOf(IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Marks).ToArray();
        }

        [Test]
        public void TheCountersOfASample_AreGauged()
        {
            var running = _fixture.Collection.Begin();

            _calls.Add(TelemetryGlCallsLayout.Compiles, 3);
            _calls.Add(TelemetryGlCallsLayout.CompileMs, 12.5);
            _calls.Add(TelemetryGlCallsLayout.Links, 1);
            _calls.Add(TelemetryGlCallsLayout.LinkMs, 40);
            _calls.Add(TelemetryGlCallsLayout.BufferBytes, 4096);
            _calls.Add(TelemetryGlCallsLayout.BufferDataCalls, 2);
            _calls.Add(TelemetryGlCallsLayout.TextureBytes, 65536);
            _calls.Add(TelemetryGlCallsLayout.TextureUploads, 4);
            _fixture.Sampler.Sample();
            _fixture.Sampler.Sample();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { 3d, 0d }, ValuesOf(batches, "web.gl.shader_compiles"));
            CollectionAssert.AreEqual(new[] { 12.5, 0d }, ValuesOf(batches, "web.gl.shader_compile_ms"));
            CollectionAssert.AreEqual(new[] { 1d, 0d }, ValuesOf(batches, "web.gl.program_links"));
            CollectionAssert.AreEqual(new[] { 40d, 0d }, ValuesOf(batches, "web.gl.program_link_ms"));
            CollectionAssert.AreEqual(new[] { 4096d, 0d }, ValuesOf(batches, "web.gl.buffer_bytes"));
            CollectionAssert.AreEqual(new[] { 2d, 0d }, ValuesOf(batches, "web.gl.buffer_data_calls"));
            CollectionAssert.AreEqual(new[] { 65536d, 0d }, ValuesOf(batches, "web.gl.texture_bytes"));
            CollectionAssert.AreEqual(new[] { 4d, 0d }, ValuesOf(batches, "web.gl.texture_uploads"));
        }

        // Unity compiles most of its shaders while it loads: the first sample of a session shows them.
        [Test]
        public void TheCallsBeforeTheSession_AreInItsFirstSample()
        {
            _calls.Add(TelemetryGlCallsLayout.Compiles, 50);
            _calls.Slow(TelemetryGlCallsLayout.SlowCompile, 300);

            var running = _fixture.Collection.Begin();
            _calls.Add(TelemetryGlCallsLayout.Compiles, 2);
            _fixture.Sampler.Sample();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { 52d }, ValuesOf(batches, "web.gl.shader_compiles"));
            Assert.AreEqual(1, MarksOf(batches).Count);
        }

        [Test]
        public void ASlowCall_IsAMark_WithItsKindAndDuration()
        {
            var running = _fixture.Collection.Begin();

            _fixture.Clock.Set(1500);
            _calls.Slow(TelemetryGlCallsLayout.SlowCompile, 61.4);
            _calls.Slow(TelemetryGlCallsLayout.SlowCompileStatus, 80);
            _calls.Slow(TelemetryGlCallsLayout.SlowLink, 55);
            _calls.Slow(TelemetryGlCallsLayout.SlowLinkStatus, 320.6);
            _fixture.Sampler.Sample();

            var marks = MarksOf(_fixture.Collection.End(running));

            CollectionAssert.AreEqual(Enumerable.Repeat("webgl.shader.slow", 4), marks.Select(mark => mark.Name));
            CollectionAssert.AreEqual(new[] { "compile", "compile status", "link", "link status" }, marks.Select(mark => mark.Detail));
            CollectionAssert.AreEqual(new long?[] { 61, 80, 55, 321 }, marks.Select(mark => mark.DurationMs));
            Assert.IsTrue(marks.All(mark => mark.Time == 1500));
        }

        [Test]
        public void ManySlowCalls_OnlyTheFirstMark()
        {
            var running = _fixture.Collection.Begin();

            for (var i = 0; i < GlCallsTelemetryCollector.MaxMarks / 4 + 2; i++)
            {
                for (var j = 0; j < 4; j++)
                    _calls.Slow(TelemetryGlCallsLayout.SlowLinkStatus, 100);

                _fixture.Sampler.Sample();
            }

            Assert.AreEqual(GlCallsTelemetryCollector.MaxMarks, MarksOf(_fixture.Collection.End(running)).Count);
        }

        [Test]
        public void TheNames_AreValid()
        {
            var running = _fixture.Collection.Begin();
            _calls.Slow(TelemetryGlCallsLayout.SlowLink, 100);
            _fixture.Sampler.Sample();

            var batches = _fixture.Collection.End(running);

            foreach (var name in batches.SelectMany(batch => batch.Names).Select(name => name.Name).Concat(MarksOf(batches).Select(mark => mark.Name)))
                Assert.IsTrue(TelemetryNameGuard.IsValid(name, TelemetryLimits.MaxNameLength), name);
        }

        [Test]
        public void ADefaultThreshold_Is50Ms()
        {
            Assert.AreEqual(50d, new ClientTelemetryOptions().SlowShaderMs);
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-gl-calls.test.js");
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
