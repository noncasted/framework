using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The loss of the WebGL context without a browser: the events come from a fake page, a session is the real collection with the
    // real way of the errors. TelemetryGlContext.jslib is on node below.
    public class TelemetryGlContextTests
    {
        private const string LostMark = "webgl.context_lost";
        private const string RestoredMark = "webgl.context_restored";
        private const string LostMetric = "web.gl.context_lost";
        private const string LostError = "WebGL context lost";

        private FakeTelemetryGlContext _context;
        private TelemetryFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _context = new FakeTelemetryGlContext();
            _fixture = new TelemetryFixture(catchErrors: true, glContext: _context);
        }

        private static IEnumerable<string> MarksOf(IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Marks).Select(mark => mark.Name);
        }

        private static IEnumerable<TelemetryBatchError> ErrorsOf(IReadOnlyList<TelemetryBatch> batches)
        {
            return batches.SelectMany(batch => batch.Errors);
        }

        // By the slots of the names: the values of one metric, sample by sample.
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
        public void Lost_InASession_MarkAndErrorInThatSession()
        {
            var running = _fixture.Collection.Begin();

            _fixture.Clock.Set(61000);
            _context.Lose();

            var batches = _fixture.Collection.End(running);
            var mark = batches.SelectMany(batch => batch.Marks).Single();
            var error = ErrorsOf(batches).Single();

            Assert.AreEqual(LostMark, mark.Name);
            Assert.AreEqual(61000, mark.Time);
            Assert.AreEqual(TelemetryErrorLevel.Error, error.Level);
            Assert.AreEqual(LostError, error.Message);
            Assert.AreEqual(1, error.Repeat);
        }

        [Test]
        public void Restored_InASession_MarkWithoutAnError()
        {
            var running = _fixture.Collection.Begin();

            _context.Restore();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { RestoredMark }, MarksOf(batches));
            CollectionAssert.IsEmpty(ErrorsOf(batches));
        }

        [Test]
        public void LostAndRestored_InASession_MarksInTheirOrder()
        {
            var running = _fixture.Collection.Begin();

            _context.Lose();
            _context.Restore();
            _context.Lose();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { LostMark, RestoredMark, LostMark }, MarksOf(batches));
        }

        [Test]
        public void LostBeforeTheFirstSession_ComesOnceWithIt()
        {
            _context.Lose();

            var first = _fixture.Collection.End(_fixture.Collection.Begin());
            var second = _fixture.Collection.End(_fixture.Collection.Begin());

            CollectionAssert.AreEqual(new[] { LostMark }, MarksOf(first));
            Assert.AreEqual(LostError, ErrorsOf(first).Single().Message);
            CollectionAssert.IsEmpty(MarksOf(second));
            CollectionAssert.IsEmpty(ErrorsOf(second));
        }

        // The queue of the errors is closed between the sessions: the next one has to open it before it takes this loss.
        [Test]
        public void LostBetweenTheSessions_ComesOnceWithTheNext()
        {
            var first = _fixture.Collection.End(_fixture.Collection.Begin());

            _context.Lose();

            var second = _fixture.Collection.End(_fixture.Collection.Begin());
            var third = _fixture.Collection.End(_fixture.Collection.Begin());

            CollectionAssert.IsEmpty(MarksOf(first));
            CollectionAssert.AreEqual(new[] { LostMark }, MarksOf(second));
            Assert.AreEqual(LostError, ErrorsOf(second).Single().Message);
            CollectionAssert.IsEmpty(MarksOf(third));
            CollectionAssert.IsEmpty(ErrorsOf(third));
        }

        [Test]
        public void LostAndRestoredWithoutASession_BothMarksOnceInTheNext()
        {
            _context.Lose();
            _context.Restore();
            _context.Lose();
            _context.Restore();

            var batches = _fixture.Collection.End(_fixture.Collection.Begin());

            CollectionAssert.AreEqual(new[] { LostMark, RestoredMark }, MarksOf(batches));
            Assert.AreEqual(1, ErrorsOf(batches).Count());
        }

        // A second scope reads the same page: what the first one took is gone.
        [Test]
        public void TheNextScope_DoesNotRepeatWhatIsTaken()
        {
            _context.Lose();
            _fixture.Collection.End(_fixture.Collection.Begin());

            var nextScope = new TelemetryFixture(catchErrors: true, glContext: _context);
            var batches = nextScope.Collection.End(nextScope.Collection.Begin());

            CollectionAssert.IsEmpty(MarksOf(batches));
            CollectionAssert.IsEmpty(ErrorsOf(batches));
        }

        [Test]
        public void LostAgainInASession_OneRecordWithTheRepeat()
        {
            var running = _fixture.Collection.Begin();

            _context.Lose();
            _context.Lose();

            var batches = _fixture.Collection.End(running);
            var error = ErrorsOf(batches).Single();

            Assert.AreEqual(LostError, error.Message);
            Assert.AreEqual(2, error.Repeat);
        }

        [Test]
        public void Metric_FollowsTheContextSampleBySample()
        {
            var running = _fixture.Collection.Begin();

            _fixture.Collection.Sample();
            _context.Lose();
            _fixture.Collection.Sample();
            _fixture.Collection.Sample();
            _context.Restore();
            _fixture.Collection.Sample();

            var batches = _fixture.Collection.End(running);

            CollectionAssert.AreEqual(new[] { 0d, 1d, 1d, 0d }, ValuesOf(batches, LostMetric));
        }

        [Test]
        public void LostAfterTheSession_IsNotInItsLastBatches_ButInTheNext()
        {
            var running = _fixture.Collection.Begin();
            var first = _fixture.Collection.End(running);

            _context.Lose();

            CollectionAssert.IsEmpty(MarksOf(first));
            Assert.AreEqual(0, _fixture.Buffer.Marks.Count);

            CollectionAssert.AreEqual(new[] { LostMark }, MarksOf(_fixture.Collection.End(_fixture.Collection.Begin())));
        }

        [Test]
        public void Names_AreTheOnesTheServerTakes()
        {
            foreach (var name in new[] { LostMark, RestoredMark, LostMetric })
                Assert.IsTrue(TelemetryNameGuard.IsValid(name, TelemetryLimits.MaxNameLength), name);
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-gl-context.test.js");
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
