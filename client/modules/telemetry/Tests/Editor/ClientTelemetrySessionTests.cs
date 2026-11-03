using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The life of a session on real short intervals, over a transport that records the calls.
    public class ClientTelemetrySessionTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private static async UniTask Wait(Func<bool> condition)
        {
            await UniTask.WaitUntil(condition).Timeout(_timeout);
        }

        private static UniTask Settle()
        {
            return UniTask.Delay(100, DelayType.Realtime);
        }

        // Without a session.

        [UnityTest]
        public IEnumerator NoSession_NothingCollectedNothingSent() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { live });

            fixture.Telemetry.Mark("a");
            Debug.LogError("Nobody listens");
            await Settle();

            Assert.IsEmpty(fixture.Transport.Calls);
            Assert.AreEqual(0, fixture.Collector.Calls);
            Assert.AreEqual(0, live.Starts);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
            Assert.AreEqual(0, fixture.ErrorQueue.Count);
        });

        [UnityTest]
        public IEnumerator NoSession_ErrorsDoNotPileUp() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryFixture(catchErrors: true);

            for (var i = 0; i < 10; i++)
                Debug.LogError($"Before the session {i}");

            await Settle();

            Assert.AreEqual(0, fixture.ErrorQueue.Count);
            Assert.AreEqual(0, fixture.Buffer.Errors.Count);
            Assert.IsEmpty(fixture.Transport.Calls);
        });

        [Test]
        public void OffInEditor_NothingStarts()
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(enabledInEditor: false, liveCollectors: new ITelemetryLiveCollector[] { live });
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            fixture.Telemetry.Mark("a");
            lifetime.Terminate();

            Assert.IsEmpty(fixture.Transport.Calls);
            Assert.AreEqual(0, live.Starts);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
        }

        // An invalid name stays a throw for the programmer who passed it even when the telemetry is off in the editor.
        [Test]
        public void OffInEditor_InvalidNames_StillThrow()
        {
            var fixture = new TelemetryFixture(enabledInEditor: false);

            Assert.Throws<ArgumentException>(() => fixture.Telemetry.Mark("Bad Name"));
            Assert.Throws<ArgumentException>(() => fixture.Telemetry.StartSession(new Lifetime(), "Bad Tag"));
        }

        // The start.

        [UnityTest]
        public IEnumerator Start_TagsAndDevice_ThenBatchesUnderTheIdOfTheServer() => UniTask.ToCoroutine(async () =>
        {
            var device = new TelemetryDevice(cores: 4);
            var fixture = new TelemetryFixture(device: device);
            var lifetime = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            fixture.Telemetry.StartSession(lifetime, "menu", "second");
            await Wait(() => fixture.Transport.Batches.Count >= 2);
            lifetime.Terminate();

            var start = fixture.Transport.Calls[0];
            Assert.AreEqual(TelemetryCallKind.Start, start.Kind);
            CollectionAssert.AreEqual(new[] { "menu", "second" }, start.Start.Tags);
            Assert.AreSame(device, start.Start.Device);
            Assert.IsTrue(fixture.Transport.Batches.All(call => call.SessionId == RecordingTransport.IdOf(1)));
        });

        [UnityTest]
        public IEnumerator Start_NoTags_IsASessionToo() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime);
            await Wait(() => fixture.Transport.Starts.Count == 1);
            lifetime.Terminate();

            Assert.IsEmpty(fixture.Transport.Starts[0].Start.Tags);
        });

        [Test]
        public void Start_InvalidTag_DevelopmentThrows_TheOpenSessionStaysOpen()
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();
            fixture.Telemetry.StartSession(lifetime, "menu");

            Assert.Throws<ArgumentException>(() => fixture.Telemetry.StartSession(new Lifetime(), "Bad Tag"));

            fixture.Telemetry.Mark("a");
            Assert.AreEqual(1, fixture.Buffer.Marks.Count);

            lifetime.Terminate();
        }

        [Test]
        public void Start_InvalidTagAndOverTheLimit_ReleaseDropsThem()
        {
            var fixture = new TelemetryFixture(strictNames: false);
            var lifetime = new Lifetime();
            var tags = Enumerable.Range(0, TelemetryLimits.MaxTagsPerSession + 2).Select(i => $"t{i}").Append("Bad Tag").ToArray();

            fixture.Telemetry.StartSession(lifetime, tags);
            lifetime.Terminate();

            CollectionAssert.AreEqual(
                tags.Take(TelemetryLimits.MaxTagsPerSession),
                fixture.Transport.Starts.Single().Start.Tags);
        }

        [Test]
        public void Start_OverTheLimitOfTags_DevelopmentThrows()
        {
            var fixture = new TelemetryFixture();
            var tags = Enumerable.Range(0, TelemetryLimits.MaxTagsPerSession + 1).Select(i => $"t{i}").ToArray();

            Assert.Throws<ArgumentException>(() => fixture.Telemetry.StartSession(new Lifetime(), tags));
        }

        [UnityTest]
        public IEnumerator Start_LifetimeAlreadyOver_NothingSent() => UniTask.ToCoroutine(async () =>
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { live });
            var lifetime = new Lifetime();
            lifetime.Terminate();

            fixture.Telemetry.StartSession(lifetime, "menu");
            fixture.Telemetry.Mark("a");
            await Settle();

            Assert.IsEmpty(fixture.Transport.Calls);
            Assert.IsTrue(live.Running.IsTerminated);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
        });

        // The end.

        [UnityTest]
        public IEnumerator End_ByLifetime_LastDataThenEnd_NothingAfter() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture(sendIntervalSeconds: 100f, sampleIntervalSeconds: 100f);
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            fixture.Telemetry.Mark("just.before.the.end");
            lifetime.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 1);

            var calls = fixture.Transport.Calls;
            CollectionAssert.AreEqual(
                new[] { TelemetryCallKind.Start, TelemetryCallKind.Batch, TelemetryCallKind.End },
                calls.Select(call => call.Kind));
            Assert.AreEqual("just.before.the.end", calls[1].Batch.Marks.Single().Name);
            Assert.AreEqual(RecordingTransport.IdOf(1), calls[2].SessionId);

            fixture.Telemetry.Mark("after");
            var collected = fixture.Collector.Calls;
            await Settle();

            Assert.AreEqual(3, calls.Count);
            Assert.AreEqual(collected, fixture.Collector.Calls);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);
        });

        // More than one batch can carry at the end: nothing is lost, the end goes last.
        [UnityTest]
        public IEnumerator End_MoreThanOneBatchLeft_AllGoBeforeTheEnd() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture(sendIntervalSeconds: 100f, sampleIntervalSeconds: 100f);
            var lifetime = new Lifetime();
            fixture.Telemetry.StartSession(lifetime, "run");

            for (var i = 0; i < TelemetryLimits.MaxErrorsPerBatch; i++)
                fixture.Errors.Add(TelemetryErrorLevel.Error, new string('m', TelemetryLimits.MaxMessageLength), new string('s', TelemetryLimits.MaxStackLength), 1);

            lifetime.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 1);

            Assert.Greater(fixture.Transport.Batches.Count, 1);
            Assert.AreEqual(TelemetryLimits.MaxErrorsPerBatch, fixture.Transport.Batches.Sum(call => call.Batch.Errors.Count));
            Assert.AreEqual(TelemetryCallKind.End, fixture.Transport.Calls.Last().Kind);
        });

        [UnityTest]
        public IEnumerator End_NothingNew_NoBatch() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture(sendIntervalSeconds: 0.01f, sampleIntervalSeconds: 100f);
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Settle();
            lifetime.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 1);

            Assert.IsEmpty(fixture.Transport.Batches);
        });

        // Menu, run, menu.

        [UnityTest]
        public IEnumerator MenuRunMenu_ThreeSessions_TagsAndDataApart() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture(sendIntervalSeconds: 0.01f, sampleIntervalSeconds: 0.01f);
            var transport = fixture.Transport;

            var menu = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("in.menu", 1));
            fixture.Telemetry.StartSession(menu, "menu");
            fixture.Telemetry.Mark("first");
            await Wait(() => transport.BatchesOf(1).Any(batch => batch.Samples.Count > 0));

            var run = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("in.run", 2));
            fixture.Telemetry.StartSession(run, "run");
            fixture.Telemetry.Mark("second");
            await Wait(() => transport.BatchesOf(2).Any(batch => batch.Samples.Count > 0));

            var menuAgain = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("in.menu", 3));
            fixture.Telemetry.StartSession(menuAgain, "menu");
            fixture.Telemetry.Mark("third");
            await Wait(() => transport.BatchesOf(3).Any(batch => batch.Samples.Count > 0));

            menuAgain.Terminate();
            await Wait(() => transport.Ends.Count == 3);

            // Run ended the menu, the next menu ended the run: in order, the end of one before the start of the next.
            CollectionAssert.AreEqual(
                new[] { "menu", "run", "menu" },
                transport.Starts.Select(call => call.Start.Tags.Single()));
            var boundaries = transport.Calls.Where(call => call.Kind != TelemetryCallKind.Batch).Select(call => call.Kind);
            CollectionAssert.AreEqual(
                new[]
                {
                    TelemetryCallKind.Start, TelemetryCallKind.End,
                    TelemetryCallKind.Start, TelemetryCallKind.End,
                    TelemetryCallKind.Start, TelemetryCallKind.End
                },
                boundaries);

            // Each session has its own names from the first slot, its own marks, a clock of its own.
            CollectionAssert.AreEqual(new[] { "in.menu" }, transport.BatchesOf(1).SelectMany(TelemetryFixture.NamesOf));
            CollectionAssert.AreEqual(new[] { "in.run" }, transport.BatchesOf(2).SelectMany(TelemetryFixture.NamesOf));
            CollectionAssert.AreEqual(new[] { "in.menu" }, transport.BatchesOf(3).SelectMany(TelemetryFixture.NamesOf));
            CollectionAssert.AreEqual(new[] { "first" }, transport.BatchesOf(1).SelectMany(batch => batch.Marks).Select(mark => mark.Name));
            CollectionAssert.AreEqual(new[] { "second" }, transport.BatchesOf(2).SelectMany(batch => batch.Marks).Select(mark => mark.Name));
            CollectionAssert.AreEqual(new[] { "third" }, transport.BatchesOf(3).SelectMany(batch => batch.Marks).Select(mark => mark.Name));
            Assert.AreEqual(3, fixture.Clock.Restarts);

            Assert.IsTrue(transport.BatchesOf(1).SelectMany(batch => batch.Samples).All(sample => sample.Values.Single() == 1d));
            Assert.IsTrue(transport.BatchesOf(2).SelectMany(batch => batch.Samples).All(sample => sample.Values.Single() == 2d));
            Assert.IsTrue(transport.BatchesOf(3).SelectMany(batch => batch.Samples).All(sample => sample.Values.Single() == 3d));
        });

        // The lifetime of a session that a new start has closed is nothing to the module any more.
        [UnityTest]
        public IEnumerator NewStart_OldLifetimeEndsLater_NoEffect() => UniTask.ToCoroutine(async () =>
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { live });
            var menu = new Lifetime();
            var run = new Lifetime();

            fixture.Telemetry.StartSession(menu, "menu");
            var menuRunning = live.Running;
            fixture.Telemetry.StartSession(run, "run");
            var runRunning = live.Running;

            Assert.IsTrue(menuRunning.IsTerminated);
            Assert.IsFalse(runRunning.IsTerminated);

            await Wait(() => fixture.Transport.Starts.Count == 2);
            menu.Terminate();
            await Settle();

            Assert.IsFalse(runRunning.IsTerminated);
            Assert.AreEqual(1, fixture.Transport.Ends.Count);

            run.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 2);
        });

        // The flag.

        [UnityTest]
        public IEnumerator DisabledOnStart_NothingMore_NextSessionAsksAgain() => UniTask.ToCoroutine(async () =>
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { live });
            var first = new Lifetime();
            fixture.Transport.AnswerStart(TelemetrySendOutcome.Disabled);

            fixture.Telemetry.StartSession(first, "menu");
            await Settle();

            Assert.AreEqual(1, fixture.Transport.Calls.Count);
            Assert.AreEqual(0, fixture.Collector.Calls);
            Assert.IsTrue(live.Running.IsTerminated);
            Assert.IsFalse(first.IsTerminated);

            fixture.Telemetry.Mark("dropped");
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);

            first.Terminate();
            await Settle();
            Assert.AreEqual(1, fixture.Transport.Calls.Count, "No end for a session the server did not open");

            var second = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("a", 1));
            fixture.Telemetry.StartSession(second, "run");
            await Wait(() => fixture.Transport.Batches.Count > 0);
            second.Terminate();

            Assert.AreEqual(2, fixture.Transport.Starts.Count);
        });

        [UnityTest]
        public IEnumerator DisabledOnBatch_NothingMore_NextSessionAsksAgain() => UniTask.ToCoroutine(async () =>
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { live });
            var first = new Lifetime();
            fixture.Transport.AnswerBatch(TelemetrySendOutcome.Disabled);
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            fixture.Telemetry.StartSession(first, "menu");
            await Wait(() => fixture.Transport.Batches.Count == 1);
            await Settle();

            Assert.AreEqual(1, fixture.Transport.Batches.Count);
            Assert.IsEmpty(fixture.Transport.Ends);
            Assert.IsTrue(live.Running.IsTerminated);

            var collected = fixture.Collector.Calls;
            await Settle();
            Assert.AreEqual(collected, fixture.Collector.Calls);

            first.Terminate();
            await Settle();
            Assert.IsEmpty(fixture.Transport.Ends);

            var second = new Lifetime();
            fixture.Telemetry.StartSession(second, "run");
            await Wait(() => fixture.Transport.Batches.Count > 1);
            second.Terminate();

            Assert.AreEqual(2, fixture.Transport.Starts.Count);
        });

        // A failure of the transport.

        [UnityTest]
        public IEnumerator FailedOnStart_NoRepeat_NothingKept_NextSessionWorks() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture();
            var first = new Lifetime();
            fixture.Transport.AnswerStart(TelemetrySendOutcome.Failed);
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            fixture.Telemetry.StartSession(first, "menu");
            fixture.Telemetry.Mark("lost");
            await Settle();

            Assert.AreEqual(1, fixture.Transport.Calls.Count);
            Assert.AreEqual(0, fixture.Buffer.Samples.Count);
            Assert.AreEqual(0, fixture.Buffer.Marks.Count);

            var second = new Lifetime();
            fixture.Telemetry.StartSession(second, "run");
            await Wait(() => fixture.Transport.Batches.Count > 0);
            second.Terminate();

            Assert.IsTrue(fixture.Transport.Batches.SelectMany(call => call.Batch.Marks).All(mark => mark.Name != "lost"));
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator FailedOnBatch_NoRepeat_NothingKept_NoEnd() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();
            fixture.Transport.AnswerBatch(TelemetrySendOutcome.Failed);
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => fixture.Transport.Batches.Count == 1);
            await Settle();
            lifetime.Terminate();
            await Settle();

            Assert.AreEqual(1, fixture.Transport.Batches.Count);
            Assert.IsEmpty(fixture.Transport.Ends);
            Assert.AreEqual(0, fixture.Buffer.Samples.Count);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator TransportThrows_NotOutside_OneLine_NextSessionAsks() => UniTask.ToCoroutine(async () =>
        {
            var transport = new ThrowingTransport();
            var fixture = new TelemetryFixture(transport: transport);
            var lifetime = new Lifetime();
            LogAssert.Expect(LogType.Exception, new Regex("bug in the transport"));

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Settle();

            Assert.AreEqual(1, transport.Calls);
            Assert.AreEqual(0, fixture.Collector.Calls);

            LogAssert.Expect(LogType.Exception, new Regex("bug in the transport"));
            fixture.Telemetry.StartSession(lifetime, "run");
            await Settle();

            Assert.AreEqual(2, transport.Calls);
            lifetime.Terminate();
        });

        // What the telemetry itself logs does not come back as an error of the game.
        [UnityTest]
        public IEnumerator TransportThrows_NotInTheErrorsOfTheSession() => UniTask.ToCoroutine(async () =>
        {
            var transport = new ThrowingTransport();
            var fixture = new TelemetryFixture(transport: transport, catchErrors: true);
            var lifetime = new Lifetime();
            LogAssert.Expect(LogType.Exception, new Regex("bug in the transport"));

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Settle();

            Assert.AreEqual(0, fixture.ErrorQueue.Count);
            lifetime.Terminate();
        });

        // One request in flight.

        [UnityTest]
        public IEnumerator InFlight_OneAtATime_AcrossSessions() => UniTask.ToCoroutine(async () =>
        {
            var transport = new GatedTransport();
            var fixture = new TelemetryFixture(transport: transport);
            var menu = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            fixture.Telemetry.StartSession(menu, "menu");
            await Wait(() => transport.Calls.Count == 1);
            transport.Answer();
            await Wait(() => transport.Calls.Count == 2);
            Assert.AreEqual(TelemetryCallKind.Batch, transport.Calls[1].Kind);

            // A batch of the menu is in flight: the run opens at once for the game, and waits its turn on the transport.
            var run = new Lifetime();
            fixture.Telemetry.StartSession(run, "run");
            fixture.Telemetry.Mark("in.run");
            await Settle();

            Assert.AreEqual(2, transport.Calls.Count);

            // Bounded: the menu has a few calls left, and a menu that keeps sending would hold this loop for the 180 s of NUnit.
            var answers = 0;

            while (transport.Calls.All(call => call.Kind != TelemetryCallKind.Start || call.Start.Tags[0] != "run"))
            {
                Assert.Less(answers++, 20, "The session of the run does not start: the menu is still sending");
                transport.Answer();
                await Wait(() => transport.InFlight == 1);
            }

            transport.Answer();
            await Wait(() => transport.Calls.Last().Kind == TelemetryCallKind.Batch);

            Assert.AreEqual(1, transport.MaxInFlight);
            Assert.AreEqual(1, transport.Calls.Count(call => call.Kind == TelemetryCallKind.End));
            Assert.AreEqual(RecordingTransport.IdOf(1), transport.Calls.Single(call => call.Kind == TelemetryCallKind.End).SessionId);

            var marks = transport.Calls
                .Where(call => call.Kind == TelemetryCallKind.Batch && call.SessionId == RecordingTransport.IdOf(2))
                .SelectMany(call => call.Batch.Marks);
            Assert.AreEqual("in.run", marks.First().Name);

            run.Terminate();
        });

        // A session that was over before its turn came: nothing of it goes, the next one goes on.
        [UnityTest]
        public IEnumerator InFlight_SessionOverBeforeItsTurn_NothingOfIt() => UniTask.ToCoroutine(async () =>
        {
            var transport = new GatedTransport();
            var fixture = new TelemetryFixture(transport: transport, sendIntervalSeconds: 100f);
            var menu = new Lifetime();
            var run = new Lifetime();
            var result = new Lifetime();

            fixture.Telemetry.StartSession(menu, "menu");
            await Wait(() => transport.Calls.Count == 1);

            fixture.Telemetry.StartSession(run, "run");
            fixture.Telemetry.StartSession(result, "result");
            transport.Answer();
            await Wait(() => transport.Calls.Count == 2);
            Assert.AreEqual(TelemetryCallKind.End, transport.Calls[1].Kind);

            transport.Answer();
            await Wait(() => transport.Calls.Count == 3);

            Assert.AreEqual("result", transport.Calls[2].Start.Tags.Single());
            result.Terminate();
        });

        // The refusal comes for the old session, when the new one is already collecting: only the old one is over.
        [UnityTest]
        public IEnumerator InFlight_DisabledAfterTheClose_NextSessionGoesOn() => UniTask.ToCoroutine(async () =>
        {
            var transport = new GatedTransport();
            var fixture = new TelemetryFixture(transport: transport, sendIntervalSeconds: 100f);
            var menu = new Lifetime();
            var run = new Lifetime();

            fixture.Telemetry.StartSession(menu, "menu");
            await Wait(() => transport.Calls.Count == 1);
            fixture.Telemetry.StartSession(run, "run");
            fixture.Telemetry.Mark("in.run");

            transport.Answer(TelemetrySendOutcome.Disabled);
            await Wait(() => transport.Calls.Count == 2);

            Assert.AreEqual("run", transport.Calls[1].Start.Tags.Single());
            Assert.AreEqual(1, fixture.Buffer.Marks.Count);
            run.Terminate();
        });

        // No queue while the answer is late: the buffer is bounded and the oldest are lost.
        [UnityTest]
        public IEnumerator InFlight_LongWait_BufferBounded() => UniTask.ToCoroutine(async () =>
        {
            var transport = new GatedTransport();
            var fixture = new TelemetryFixture(transport: transport, bufferedSamples: 3);
            var lifetime = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => fixture.Collector.Calls > 5);

            Assert.AreEqual(1, transport.Calls.Count);
            Assert.AreEqual(3, fixture.Buffer.Samples.Count);
            lifetime.Terminate();
        });

        // Errors.

        [UnityTest]
        public IEnumerator Errors_InsideTheSessionOnly_CountersPerSession() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryFixture(catchErrors: true, sendIntervalSeconds: 100f, sampleIntervalSeconds: 100f);
            var transport = fixture.Transport;

            Debug.LogError("Before the session");

            var menu = new Lifetime();
            fixture.Telemetry.StartSession(menu, "menu");
            LogSameError(2);
            menu.Terminate();
            await Wait(() => transport.Ends.Count == 1);

            Debug.LogError("Between the sessions");

            var run = new Lifetime();
            fixture.Telemetry.StartSession(run, "run");
            LogSameError(1);
            run.Terminate();
            await Wait(() => transport.Ends.Count == 2);

            Debug.LogError("After the sessions");
            await Settle();

            var inMenu = transport.BatchesOf(1).SelectMany(batch => batch.Errors).Single();
            StringAssert.Contains("Same error", inMenu.Message);
            Assert.AreEqual(2, inMenu.Repeat);
            Assert.AreEqual(TelemetryErrorLevel.Error, inMenu.Level);

            var inRun = transport.BatchesOf(2).SelectMany(batch => batch.Errors).Single();
            StringAssert.Contains("Same error", inRun.Message);
            Assert.AreEqual(1, inRun.Repeat, "The counter of the previous session does not come along");
            Assert.AreEqual(0, fixture.ErrorQueue.Count);
        });

        [UnityTest]
        public IEnumerator Errors_TextIsScrubbed() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryFixture(catchErrors: true, sendIntervalSeconds: 100f, sampleIntervalSeconds: 100f);
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            Debug.LogError("Failed for player@mail.com");
            lifetime.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 1);

            var error = fixture.Transport.Batches.SelectMany(call => call.Batch.Errors).Single();
            StringAssert.DoesNotContain("player@mail.com", error.Message);
            StringAssert.Contains("Failed for", error.Message);
        });

        // A session that has spent the limit of the errors does not close the way of the errors of the next one.
        [UnityTest]
        public IEnumerator Errors_LimitSpent_NextSessionTakesErrorsAgain() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.ignoreFailingMessages = true;
            var fixture = new TelemetryFixture(catchErrors: true, sendIntervalSeconds: 100f, sampleIntervalSeconds: 100f);
            var first = new Lifetime();

            fixture.Telemetry.StartSession(first, "menu");
            fixture.ErrorQueue.Close();
            first.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 1);

            var second = new Lifetime();
            fixture.Telemetry.StartSession(second, "run");
            Debug.LogError("Of the next session");
            second.Terminate();
            await Wait(() => fixture.Transport.Ends.Count == 2);

            StringAssert.Contains("Of the next session", fixture.Transport.BatchesOf(2).SelectMany(batch => batch.Errors).Single().Message);
        });

        // Collectors.

        [Test]
        public void LiveCollectors_OnTheLifetimeOfTheSession_StartedAnewForEach()
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { live });
            var first = new Lifetime();

            fixture.Telemetry.StartSession(first, "menu");
            var firstRunning = live.Running;
            Assert.AreEqual(1, live.Starts);
            Assert.IsFalse(firstRunning.IsTerminated);

            first.Terminate();
            Assert.IsTrue(firstRunning.IsTerminated);

            var second = new Lifetime();
            fixture.Telemetry.StartSession(second, "run");
            Assert.AreEqual(2, live.Starts);
            Assert.AreNotSame(firstRunning, live.Running);
            Assert.IsFalse(live.Running.IsTerminated);

            second.Terminate();
        }

        // The isolation of subscribers, as in the sampler: a collector of the game does not take the others down.
        [Test]
        public void LiveCollector_Throws_TheOthersStart()
        {
            var live = new RecordingLiveCollector();
            var fixture = new TelemetryFixture(liveCollectors: new ITelemetryLiveCollector[] { new ThrowingLiveCollector(), live });
            var lifetime = new Lifetime();
            LogAssert.Expect(LogType.Exception, new Regex("broken collector"));

            fixture.Telemetry.StartSession(lifetime, "menu");
            lifetime.Terminate();

            Assert.AreEqual(1, live.Starts);
        }

        [UnityTest]
        public IEnumerator Sampling_OnlyWhileTheSessionIsOpen() => UniTask.ToCoroutine(async () =>
        {
            var fixture = new TelemetryFixture();
            var lifetime = new Lifetime();
            fixture.Collector.Script(sample => sample.Gauge("a", 1));

            await Settle();
            Assert.AreEqual(0, fixture.Collector.Calls);

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => fixture.Collector.Calls >= 2);
            lifetime.Terminate();
            await UniTask.Yield();

            var calls = fixture.Collector.Calls;
            await Wait(() => fixture.Transport.Ends.Count == 1);
            await Settle();

            Assert.AreEqual(calls, fixture.Collector.Calls);
            LogAssert.NoUnexpectedReceived();
        });

        // A hidden tab at the end of one session is not the first sample of the next.
        [Test]
        public void HiddenOfThePreviousSession_NotInTheNext()
        {
            var fixture = new TelemetryFixture(sendIntervalSeconds: 100f, sampleIntervalSeconds: 100f);
            var first = new Lifetime();
            fixture.Telemetry.StartSession(first, "menu");
            fixture.Page.Hide();
            fixture.Page.Show();
            first.Terminate();

            var second = new Lifetime();
            fixture.Telemetry.StartSession(second, "run");
            fixture.Sample(1000, sample => sample.Gauge("a", 1));

            Assert.IsFalse(fixture.Writer.WriteNext().Samples.Single().IsHidden);
            second.Terminate();
        }

        [Test]
        public void CollectorThrows_OthersStillCollected()
        {
            var good = new ScriptedCollector();
            good.Script(sample => sample.Gauge("good", 1));
            var fixture = new TelemetryFixture(collectors: new ITelemetryCollector[] { new ThrowingCollector(), good });
            fixture.Buffer.Open();
            LogAssert.Expect(LogType.Exception, new Regex("bug in the collector"));

            fixture.Sampler.Sample();

            Assert.AreEqual(1, good.Calls);
            CollectionAssert.AreEqual(new[] { "good" }, TelemetryFixture.NamesOf(fixture.Writer.WriteNext()));
        }

        // One place in the code, one stack: the two errors are one.
        private static void LogSameError(int times)
        {
            for (var i = 0; i < times; i++)
                Debug.LogError("Same error");
        }

        private class RecordingLiveCollector : ITelemetryLiveCollector
        {
            public int Starts { get; private set; }
            public IReadOnlyLifetime Running { get; private set; }

            public void Start(IReadOnlyLifetime running)
            {
                Starts++;
                Running = running;
            }
        }

        private class ThrowingLiveCollector : ITelemetryLiveCollector
        {
            public void Start(IReadOnlyLifetime running)
            {
                throw new InvalidOperationException("broken collector");
            }
        }

        private class ThrowingCollector : ITelemetryCollector
        {
            public void Collect(ITelemetrySample sample)
            {
                throw new InvalidOperationException("bug in the collector");
            }
        }
    }
}
