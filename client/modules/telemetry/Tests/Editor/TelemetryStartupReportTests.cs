using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // What goes to the transport and when: the snapshot, the final trace, the id of the record and the end of the traces of a connection.
    public class TelemetryStartupReportTests
    {
        private static readonly Guid _first = RecordingTransport.StartupIdOf(1);
        private static readonly Guid _second = RecordingTransport.StartupIdOf(2);

        private readonly Lifetime _lifetime = new Lifetime();

        private static ProfilerTraceData Open() => FakeTelemetryStartupSource.Trace("Open");
        private static ProfilerTraceData Closed() => FakeTelemetryStartupSource.Trace("Closed");

        private TelemetryFixture Fixture(ITelemetryTransport transport = null, bool enabledInEditor = true)
        {
            var fixture = new TelemetryFixture(enabledInEditor: enabledInEditor, transport: transport);

            fixture.Startup.OnSetup(_lifetime);
            fixture.StartupSource.Open(Open());

            return fixture;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void Snapshot_ThenFinal_SameRecord()
        {
            var fixture = Fixture();

            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());

            var sent = fixture.Transport.Startups;
            Assert.AreEqual(2, sent.Count);
            Assert.IsFalse(sent[0].IsFinal);
            Assert.AreEqual("Open", sent[0].Name);
            Assert.AreEqual(Guid.Empty, sent[0].StartupId);
            Assert.IsTrue(sent[1].IsFinal);
            Assert.AreEqual("Closed", sent[1].Name);
            Assert.AreEqual(_first, sent[1].StartupId);
        }

        [Test]
        public void Final_BeforeTheCallOfTheGame_WaitsAndGoesWithoutASnapshot()
        {
            var fixture = Fixture();

            fixture.StartupSource.Close(Closed());
            Assert.IsEmpty(fixture.Transport.Startups, "the socket may not be authorized yet");

            fixture.Telemetry.SendStartupTrace();

            var sent = fixture.Transport.Startups.Single();
            Assert.IsTrue(sent.IsFinal);
            Assert.AreEqual(Guid.Empty, sent.StartupId);
            Assert.AreEqual(0, fixture.StartupSource.Snapshots);
        }

        [Test]
        public void Final_OnALostConnection_GoesOnTheNextOne()
        {
            var fixture = Fixture();

            fixture.Telemetry.SendStartupTrace();
            fixture.Transport.CloseLink();
            fixture.StartupSource.Close(Closed());
            Assert.AreEqual(1, fixture.Transport.Startups.Count);

            fixture.Transport.OpenLink();
            fixture.Telemetry.SendStartupTrace();

            var sent = fixture.Transport.Startups;
            Assert.AreEqual(2, sent.Count);
            Assert.IsTrue(sent[1].IsFinal);
            Assert.AreEqual(Guid.Empty, sent[1].StartupId, "the id died with its connection");
        }

        [Test]
        public void Call_WithoutAConnection_NothingAndOneLine()
        {
            var fixture = Fixture();

            fixture.Transport.CloseLink();
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[Telemetry\\]"));
            fixture.Telemetry.SendStartupTrace();

            Assert.IsEmpty(fixture.Transport.Startups);
        }

        [Test]
        public void Disabled_NoMoreTracesOnThisConnection_OneLine()
        {
            var fixture = Fixture();

            fixture.Transport.AnswerStartup(TelemetrySendOutcome.Disabled);
            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());
            Assert.AreEqual(1, fixture.Transport.Startups.Count);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[Telemetry\\]"));
            fixture.Telemetry.SendStartupTrace();

            Assert.AreEqual(1, fixture.Transport.Startups.Count);
            Assert.AreEqual(Guid.Empty, fixture.Startup.Id);
        }

        [Test]
        public void Disabled_TheSessionGoesOn()
        {
            var fixture = Fixture();
            var session = new Lifetime();

            fixture.Transport.AnswerStartup(TelemetrySendOutcome.Disabled);
            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.StartSession(session, "menu");
            session.Terminate();

            Assert.AreEqual(Guid.Empty, fixture.Transport.Starts.Single().Start.StartupId);
        }

        [Test]
        public void Failed_FinalIsNotRepeatedOnThisConnection_GoesOnTheNext()
        {
            var fixture = Fixture();

            fixture.Transport.AnswerStartup(TelemetrySendOutcome.Failed);
            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());
            Assert.AreEqual(1, fixture.Transport.Startups.Count);

            fixture.Transport.OpenLink();
            fixture.Telemetry.SendStartupTrace();

            Assert.AreEqual(2, fixture.Transport.Startups.Count);
            Assert.IsTrue(fixture.Transport.Startups[1].IsFinal);
        }

        [Test]
        public void Bug_OfTheTransport_OneException_GameGoesOn()
        {
            var transport = new ThrowingTransport();
            var fixture = Fixture(transport);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("bug in the transport"));
            fixture.Telemetry.SendStartupTrace();

            Assert.AreEqual(1, transport.Calls);
        }

        [Test]
        public void StartSession_GetsTheId()
        {
            var fixture = Fixture();
            var session = new Lifetime();

            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.StartSession(session, "menu");
            session.Terminate();

            Assert.AreEqual(_first, fixture.Transport.Starts.Single().Start.StartupId);
        }

        [Test]
        public void StartSession_NoTrace_NoId()
        {
            var fixture = Fixture();
            var session = new Lifetime();

            fixture.Telemetry.StartSession(session, "menu");
            session.Terminate();

            Assert.AreEqual(Guid.Empty, fixture.Transport.Starts.Single().Start.StartupId);
        }

        [UnityTest]
        public IEnumerator Reconnect_ForgetsTheId_NewRecord() => UniTask.ToCoroutine(async () =>
        {
            var fixture = Fixture();
            var before = new Lifetime();
            var after = new Lifetime();

            fixture.Telemetry.SendStartupTrace();
            fixture.Transport.OpenLink();
            Assert.AreEqual(Guid.Empty, fixture.Startup.Id);

            fixture.Telemetry.StartSession(before, "menu");
            before.Terminate();
            Assert.AreEqual(Guid.Empty, fixture.Transport.Starts[0].Start.StartupId);

            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.StartSession(after, "menu");
            await UniTask.WaitUntil(() => fixture.Transport.Starts.Count == 2).Timeout(TimeSpan.FromSeconds(10));
            after.Terminate();

            var sent = fixture.Transport.Startups;
            Assert.AreEqual(2, sent.Count);
            Assert.AreEqual(Guid.Empty, sent[1].StartupId);
            Assert.AreEqual(_second, fixture.Transport.Starts[1].Start.StartupId);
        });

        [Test]
        public void Repeated_OnOneConnection_NoSecondWrite()
        {
            var fixture = Fixture();

            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[Telemetry\\]"));
            fixture.Telemetry.SendStartupTrace();

            var sent = fixture.Transport.Startups;
            Assert.AreEqual(2, sent.Count);
            Assert.IsFalse(sent[0].IsFinal);
            Assert.IsTrue(sent[1].IsFinal);
        }

        [Test]
        public void OffInEditor_NoCall()
        {
            var fixture = Fixture(enabledInEditor: false);

            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());

            Assert.IsEmpty(fixture.Transport.Calls);
        }

        [Test]
        public void EndOfTheLifetime_FinalNoLongerListened()
        {
            var lifetime = new Lifetime();
            var fixture = new TelemetryFixture();

            fixture.Startup.OnSetup(lifetime);
            fixture.StartupSource.Open(Open());
            fixture.Telemetry.SendStartupTrace();
            lifetime.Terminate();
            fixture.StartupSource.Close(Closed());

            Assert.AreEqual(1, fixture.Transport.Startups.Count);
        }

        // The session waits for the answer in flight, gets its id, and the requests never overlap.
        [Test]
        public void Gate_SessionWaitsForTheTrace_OneInFlight()
        {
            var transport = new GatedTransport();
            var fixture = Fixture(transport);
            var session = new Lifetime();

            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.StartSession(session, "menu");
            Assert.AreEqual(1, transport.Calls.Count, "the start waits for the answer to the trace");

            transport.Answer();

            Assert.AreEqual(2, transport.Calls.Count);
            Assert.AreEqual(TelemetryCallKind.Start, transport.Calls[1].Kind);
            Assert.AreEqual(_first, transport.Calls[1].Start.StartupId);
            Assert.AreEqual(1, transport.MaxInFlight);
            session.Terminate();
            transport.Answer();
        }

        // A final trace that closes while the answer to the snapshot is on its way goes after it, with its id.
        [Test]
        public void Gate_FinalAfterTheAnswerToTheSnapshot()
        {
            var transport = new GatedTransport();
            var fixture = Fixture(transport);

            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());
            Assert.AreEqual(1, transport.Calls.Count);

            transport.Answer();

            Assert.AreEqual(2, transport.Calls.Count);
            Assert.IsTrue(transport.Calls[1].StartupTrace.IsFinal);
            Assert.AreEqual(_first, transport.Calls[1].StartupTrace.StartupId);
            transport.Answer();
        }

        // A snapshot waiting for its turn when the trace closes: the final one takes its place.
        [Test]
        public void Gate_ClosedBeforeTheTurn_OnlyTheFinal()
        {
            var transport = new GatedTransport();
            var fixture = Fixture(transport);
            var session = new Lifetime();

            fixture.Telemetry.StartSession(session, "menu");
            fixture.Telemetry.SendStartupTrace();
            fixture.StartupSource.Close(Closed());
            transport.Answer();

            var startups = transport.Calls.Where(call => call.Kind == TelemetryCallKind.Startup).ToArray();
            Assert.AreEqual(1, startups.Length);
            Assert.IsTrue(startups[0].StartupTrace.IsFinal);
            transport.Answer();
            session.Terminate();
        }

        // Closed while waiting for the trace: the start of the session never goes.
        [Test]
        public void Gate_SessionClosedInTheQueue_NoStart()
        {
            var transport = new GatedTransport();
            var fixture = Fixture(transport);
            var session = new Lifetime();

            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.StartSession(session, "menu");
            session.Terminate();
            transport.Answer();

            Assert.IsFalse(transport.Calls.Any(call => call.Kind == TelemetryCallKind.Start));
        }
    }
}
