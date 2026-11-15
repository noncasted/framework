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
            // A dead page samples no more: its own sampling would write its trace again over the shared store.
            var dead = new TelemetryFixture(sampleIntervalSeconds: 100f);
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

        // The session that reported the trace closed before its start was answered: it opens no trace of its own, and the
        // reported one must still be gone, or the next start reports the same lost session again.
        [UnityTest]
        public IEnumerator ATraceReportedByASessionClosedBeforeItsAnswer_IsReportedOnce() => UniTask.ToCoroutine(async () =>
        {
            // A dead page samples no more: its own sampling would write its trace again over the shared store.
            var dead = new TelemetryFixture(sampleIntervalSeconds: 100f);
            dead.Telemetry.StartSession(new Lifetime(), "run");

            var transport = new GatedTransport();
            var next = new TelemetryFixture(transport: transport, sendIntervalSeconds: 100f, traceStore: dead.TraceStore);
            var lobby = new Lifetime();

            next.Telemetry.StartSession(new Lifetime(), "menu");
            await UniTask.WaitUntil(() => transport.Calls.Count == 1).Timeout(TimeSpan.FromSeconds(10));
            Assert.IsNotNull(transport.Calls[0].Start.Previous);

            next.Telemetry.StartSession(lobby, "lobby");

            await UniTask.WaitUntil(() =>
            {
                if (transport.InFlight > 0)
                    transport.Answer();

                return transport.Calls.Count(call => call.Kind == TelemetryCallKind.Start) == 2;
            }).Timeout(TimeSpan.FromSeconds(10));

            var start = transport.Calls.Last(call => call.Kind == TelemetryCallKind.Start).Start;
            Assert.AreEqual("lobby", start.Tags.Single());
            Assert.IsNull(start.Previous);

            transport.Answer();
            lobby.Terminate();
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

        // A dead page often dies inside a load: its key and the time it ran are what the report of the next start needs.
        [Test]
        public void TheMarks_KeepTheirDetailAndDuration()
        {
            var dead = new TelemetryFixture();
            dead.Telemetry.StartSession(new Lifetime());
            dead.Buffer.AddMark(1000, "load.start", "Group: MusicRunAudio");
            dead.Buffer.AddMark(6000, "load.slow", "Group: MusicRunAudio", 5000);
            dead.Buffer.AddMark(7000, "load.failed", new string('x', TelemetryLimits.MaxMarkDetailLength + 10), 0);
            dead.Telemetry.Mark("page.pagehide");

            var next = new TelemetryFixture(traceStore: dead.TraceStore);
            next.Telemetry.StartSession(new Lifetime());

            var marks = PreviousOf(next, 0).Marks;
            Assert.AreEqual(4, marks.Count);
            Assert.AreEqual("Group: MusicRunAudio", marks[0].Detail);
            Assert.IsNull(marks[0].DurationMs);
            Assert.AreEqual("Group: MusicRunAudio", marks[1].Detail);
            Assert.AreEqual(5000, marks[1].DurationMs);
            Assert.AreEqual(TelemetryLimits.MaxMarkDetailLength, marks[2].Detail.Length);
            Assert.AreEqual(0, marks[2].DurationMs);
            Assert.AreEqual("page.pagehide", marks[3].Name);
            Assert.IsNull(marks[3].Detail);
            Assert.IsNull(marks[3].DurationMs);
        }

        // What the frames did right before the page died: the main numbers of the last samples, by the names of the trace.
        [Test]
        public void TheSamples_AreTheLastOnes_WithTheMainNumbersOnly()
        {
            var dead = new TelemetryFixture();
            dead.Telemetry.StartSession(new Lifetime());

            for (var i = 0; i < TelemetrySessionTrace.MaxSamples + 2; i++)
            {
                var value = 10d + i;
                dead.Collector.Script(sample =>
                {
                    sample.Gauge("other.metric", 1d);
                    sample.Gauge("frame.dt.max", value, TelemetryUnits.Milliseconds);
                    sample.Gauge("render.draw_calls", 140d, TelemetryUnits.Count);
                });
                dead.Clock.Set(1000 * (i + 1));
                dead.Collection.Sample();
            }

            dead.Page.Hide();
            dead.Collector.Script(sample => sample.Gauge("frame.dt.max", 950d, TelemetryUnits.Milliseconds));
            dead.Clock.Set(20000);
            dead.Collection.Sample();

            var next = new TelemetryFixture(traceStore: dead.TraceStore);
            next.Telemetry.StartSession(new Lifetime());

            var previous = PreviousOf(next, 0);
            CollectionAssert.AreEqual(TelemetrySessionTrace.SampleNames, previous.SampleNames);
            CollectionAssert.DoesNotContain(previous.SampleNames, "other.metric");
            Assert.AreEqual(TelemetrySessionTrace.MaxSamples, previous.Samples.Count);

            var dtMax = previous.SampleNames.ToList().IndexOf("frame.dt.max");
            var drawCalls = previous.SampleNames.ToList().IndexOf("render.draw_calls");
            var last = previous.Samples[previous.Samples.Count - 1];
            var beforeLast = previous.Samples[previous.Samples.Count - 2];

            Assert.AreEqual(20000, last.Time);
            Assert.IsTrue(last.IsHidden);
            Assert.AreEqual(950d, last.Values[dtMax]);
            Assert.IsTrue(double.IsNaN(last.Values[drawCalls]), "no value in that sample");
            Assert.AreEqual(10d + TelemetrySessionTrace.MaxSamples + 1, beforeLast.Values[dtMax]);
            Assert.AreEqual(140d, beforeLast.Values[drawCalls]);
            Assert.IsFalse(beforeLast.IsHidden);
            Assert.AreEqual(previous.Samples.Select(sample => sample.Time).OrderBy(time => time), previous.Samples.Select(sample => sample.Time));
        }

        // A trace written before the marks had a detail reads as marks without one.
        [Test]
        public void ATraceWithoutDetails_ReadsAsMarksWithoutThem()
        {
            var store = new MemoryTraceStore();
            store.Write("{\"id\":\"" + Guid.NewGuid() + "\",\"tags\":[],\"lastSample\":10,\"writtenAt\":0,\"markTimes\":[5],\"markNames\":[\"page.hidden\"]}");
            var fixture = new TelemetryFixture(traceStore: store);

            fixture.Telemetry.StartSession(new Lifetime());

            var previous = PreviousOf(fixture, 0);
            Assert.IsEmpty(previous.SampleNames);
            Assert.IsEmpty(previous.Samples);

            var mark = previous.Marks.Single();
            Assert.AreEqual("page.hidden", mark.Name);
            Assert.IsNull(mark.Detail);
            Assert.IsNull(mark.DurationMs);
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
