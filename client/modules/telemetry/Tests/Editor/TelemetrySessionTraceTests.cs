using System.Diagnostics;
using System.IO;
using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The trace of the open session in the storage of the browser: what the next start reports when the page died inside a session.
    public class TelemetrySessionTraceTests
    {
        // A start waits for the sending of the session before it to end.
        private static async UniTask WaitForStarts(TelemetryFixture fixture, int count)
        {
            await UniTask.WaitUntil(() => fixture.Transport.Starts.Count >= count).Timeout(TimeSpan.FromSeconds(10));
        }

        private static TelemetryPreviousSession PreviousOf(TelemetryFixture fixture, int start)
        {
            return fixture.Transport.Starts[start].Start.Previous;
        }

        [Test]
        public void AnOpenSession_IsKept_WithItsIdTagsLastSampleAndMarks()
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            fixture.Clock.Set(1500);
            fixture.Telemetry.Mark("stage.loaded");
            fixture.Clock.Set(2000);
            fixture.Collection.Sample();

            var kept = fixture.TraceStore.Text;
            StringAssert.Contains(RecordingTransport.IdOf(1).ToString(), kept);
            StringAssert.Contains("menu", kept);
            StringAssert.Contains("stage.loaded", kept);

            lifetime.Terminate();
        }

        [UnityTest]
        public IEnumerator AClosedSession_LeavesNothing_AndTheNextStartReportsNone() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture();
            var first = new Lifetime();

            fixture.Telemetry.StartSession(first, "menu");
            first.Terminate();

            Assert.IsNull(fixture.TraceStore.Text);

            var second = new Lifetime();
            fixture.Telemetry.StartSession(second, "run");
            await WaitForStarts(fixture, 2);

            Assert.IsNull(PreviousOf(fixture, 1));
            second.Terminate();
        });

        // A new start in the same page closes the open session first: only a page that died leaves its trace.
        [UnityTest]
        public IEnumerator ANewStart_ClosesTheOpenOne_WhichIsNoPrevious() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture();
            var second = new Lifetime();

            fixture.Telemetry.StartSession(new Lifetime(), "menu");
            fixture.Telemetry.StartSession(second, "run");
            await WaitForStarts(fixture, 2);

            Assert.IsNull(PreviousOf(fixture, 1));
            second.Terminate();
        });

        [UnityTest]
        public IEnumerator ATraceLeftByADeadPage_IsReportedOnce_WithTheTimeSinceIt() => UniTask.ToCoroutine(async () =>
        {
            var dead = new TelemetryFixture();
            dead.Telemetry.StartSession(new Lifetime(), "menu", "second");
            dead.Clock.Set(30500);
            dead.Telemetry.Mark("webgl.context_lost");
            dead.Clock.Set(31000);
            dead.Collection.Sample();
            dead.WallClock.Set(100_000);
            dead.Telemetry.Mark("page.pagehide");

            var next = new TelemetryFixture(traceStore: dead.TraceStore);
            next.WallClock.Set(104_000);
            next.Telemetry.StartSession(new Lifetime(), "menu");

            var previous = PreviousOf(next, 0);
            Assert.AreEqual(RecordingTransport.IdOf(1), previous.SessionId);
            CollectionAssert.AreEqual(new[] { "menu", "second" }, previous.Tags);
            Assert.AreEqual(31000, previous.LastSampleTime);
            Assert.AreEqual(4000, previous.SinceMs);
            CollectionAssert.AreEqual(new[] { "webgl.context_lost", "page.pagehide" }, previous.Marks.Select(mark => mark.Name));
            Assert.AreEqual(30500, previous.Marks[0].Time);

            var marks = Enumerable.Range(0, next.Buffer.Marks.Count).Select(index => next.Buffer.Marks.At(index).Name).ToArray();
            CollectionAssert.Contains(marks, "session.previous_unclosed");

            var last = new Lifetime();
            next.Telemetry.StartSession(last, "run");
            await WaitForStarts(next, 2);

            Assert.IsNull(PreviousOf(next, 1));
            last.Terminate();
        });

        [Test]
        public void TheMarks_AreTheLastOnes()
        {
            var dead = new TelemetryFixture();
            dead.Telemetry.StartSession(new Lifetime());

            for (var i = 0; i < TelemetrySessionTrace.MaxMarks + 3; i++)
                dead.Telemetry.Mark($"mark.{i}");

            var next = new TelemetryFixture(traceStore: dead.TraceStore);
            next.Telemetry.StartSession(new Lifetime());

            var marks = PreviousOf(next, 0).Marks;
            Assert.AreEqual(TelemetrySessionTrace.MaxMarks, marks.Count);
            Assert.AreEqual($"mark.{TelemetrySessionTrace.MaxMarks + 2}", marks[marks.Count - 1].Name);
            Assert.AreEqual("mark.3", marks[0].Name);
        }

        // A session the server did not take has no id to report.
        [Test]
        public void ARefusedStart_KeepsNoTrace()
        {
            var fixture = new TelemetryFixture();
            fixture.Transport.AnswerStart(TelemetrySendOutcome.Disabled);

            fixture.Telemetry.StartSession(new Lifetime(), "menu");

            Assert.IsNull(fixture.TraceStore.Text);
        }

        [Test]
        public void ABrokenTrace_IsDroppedWithoutAReport()
        {
            var store = new MemoryTraceStore();
            store.Write("{not json");
            var fixture = new TelemetryFixture(traceStore: store);

            fixture.Telemetry.StartSession(new Lifetime(), "menu");

            Assert.IsNull(PreviousOf(fixture, 0));
        }

        [Test]
        public void Jslib_OnNode()
        {
            var script = Path.Combine(TelemetrySources.ModuleFolder, "Tests~", "telemetry-trace.test.js");
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
