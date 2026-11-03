using System;
using System.Linq;
using Internal;
using MemoryPack;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The startup trace over the socket transport: the messages of the contract and the run of the connection as its border.
    public class TelemetryStartupSocketTests
    {
        private static readonly Guid _record = new Guid(11, 0, 0, new byte[8]);

        private readonly Lifetime _lifetime = new Lifetime();

        private static INetworkContext Server(INetworkContext request)
        {
            return request switch
            {
                SharedClientTelemetry.StartupTrace _ => new SharedClientTelemetry.StartupTraceResult { Enabled = true, StartupId = _record },
                SharedClientTelemetry.SessionStart _ => new SharedClientTelemetry.SessionStarted { Enabled = true, SessionId = Guid.NewGuid() },
                SharedClientTelemetry.Batch _ => new SharedClientTelemetry.BatchResult { Enabled = true },
                SharedClientTelemetry.SessionEnd _ => EmptyResponse.Ok,
                _ => throw new InvalidOperationException($"Unexpected {request.GetType().Name}")
            };
        }

        private TelemetryFixture Fixture(ITelemetrySocket socket)
        {
            var fixture = new TelemetryFixture(transport: new TelemetrySocketTransport(socket, new TelemetryTicks(), new TelemetryRoundTrip()));

            fixture.Startup.OnSetup(_lifetime);
            fixture.StartupSource.Open(FakeTelemetryStartupSource.Trace(
                "Open",
                new ProfilerSpanData { ParentId = -1, Name = "Root", StartMs = 1d, DurationMs = 2d, Frames = 3, Unfinished = true, AllocBytes = 4L }));

            return fixture;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void Message_AllFields_AndTheIdInTheSession()
        {
            var writer = new ScriptedTelemetryWriter(Server);
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var session = new Lifetime();

            fixture.Telemetry.SendStartupTrace();
            fixture.Telemetry.StartSession(session, "menu");
            session.Terminate();

            var trace = writer.Requests.OfType<SharedClientTelemetry.StartupTrace>().Single();
            Assert.AreEqual(Guid.Empty, trace.StartupId);
            Assert.AreEqual("Open", trace.Name);
            Assert.IsFalse(trace.Final);
            Assert.AreEqual(100d, trace.DurationMs);
            Assert.AreEqual(5, trace.Frames);
            Assert.IsNotNull(trace.Device);

            var span = trace.Spans.Single();
            Assert.AreEqual(-1, span.ParentIndex);
            Assert.AreEqual("Root", span.Name);
            Assert.AreEqual(1d, span.StartMs);
            Assert.AreEqual(2d, span.DurationMs);
            Assert.AreEqual(3, span.Frames);
            Assert.IsTrue(span.Unfinished);
            Assert.AreEqual(4L, span.AllocBytes);

            Assert.AreEqual(_record, writer.Requests.OfType<SharedClientTelemetry.SessionStart>().Single().StartupId);
        }

        // The meta socket runs the same connection object again after a loss.
        [Test]
        public void SameConnection_RunAgain_ForgetsTheId()
        {
            var writer = new ScriptedTelemetryWriter(Server);
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var session = new Lifetime();

            fixture.Telemetry.SendStartupTrace();
            Assert.AreEqual(_record, fixture.Startup.Id);

            writer.Lose();
            Assert.AreEqual(Guid.Empty, fixture.Startup.Id);

            writer.RunAgain();
            Assert.AreEqual(Guid.Empty, fixture.Startup.Id);

            fixture.Telemetry.StartSession(session, "menu");
            session.Terminate();
            Assert.AreEqual(Guid.Empty, writer.Requests.OfType<SharedClientTelemetry.SessionStart>().Single().StartupId);

            fixture.Telemetry.SendStartupTrace();

            var traces = writer.Requests.OfType<SharedClientTelemetry.StartupTrace>().ToArray();
            Assert.AreEqual(2, traces.Length);
            Assert.AreEqual(Guid.Empty, traces[1].StartupId);
        }

        [Test]
        public void ClosedSocket_NoRequest_OneLine()
        {
            var writer = new ScriptedTelemetryWriter(Server);
            var fixture = Fixture(new FakeTelemetrySocket(writer));

            writer.Lose();
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("\\[Telemetry\\]"));
            fixture.Telemetry.SendStartupTrace();

            Assert.IsEmpty(writer.Requests);
        }

        // A loss in flight is a failure of the transport, nothing more goes on this run.
        [Test]
        public void LostInFlight_NoId_NoFinal()
        {
            var writer = new ScriptedTelemetryWriter();
            var fixture = Fixture(new FakeTelemetrySocket(writer));

            fixture.Telemetry.SendStartupTrace();
            writer.Lose();
            fixture.StartupSource.Close(FakeTelemetryStartupSource.Trace("Closed"));

            Assert.AreEqual(1, writer.Requests.Count);
            Assert.AreEqual(Guid.Empty, fixture.Startup.Id);
        }

        [Test]
        public void Trace_Refused_NoId()
        {
            var writer = new ScriptedTelemetryWriter(_ => new SharedClientTelemetry.StartupTraceResult { Enabled = false, StartupId = _record });
            var fixture = Fixture(new FakeTelemetrySocket(writer));

            fixture.Telemetry.SendStartupTrace();

            Assert.AreEqual(Guid.Empty, fixture.Startup.Id);
        }

        [Test]
        public void Message_RoundTrip_MemoryPack()
        {
            var bytes = MemoryPackSerializer.Serialize(new SharedClientTelemetry.StartupTrace
            {
                StartupId = _record,
                Name = "Trace",
                Spans = new[] { new SharedClientTelemetry.StartupSpan { ParentIndex = -1, Name = "Root" } }
            });
            var copy = MemoryPackSerializer.Deserialize<SharedClientTelemetry.StartupTrace>(bytes);

            Assert.AreEqual(_record, copy.StartupId);
            Assert.AreEqual("Root", copy.Spans[0].Name);
        }
    }
}
