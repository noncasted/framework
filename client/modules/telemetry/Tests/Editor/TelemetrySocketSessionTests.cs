using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The sessions of the module over the socket transport: what the connection sees, in which order, and that a lost
    // connection costs one session and does not stop the next one.
    public class TelemetrySocketSessionTests
    {
        private static readonly Guid _id = new Guid(9, 0, 0, new byte[8]);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private static async UniTask Wait(Func<bool> condition)
        {
            await UniTask.WaitUntil(condition).Timeout(_timeout);
        }

        private static INetworkContext Enabled(INetworkContext request)
        {
            return request switch
            {
                SharedClientTelemetry.SessionStart _ => new SharedClientTelemetry.SessionStarted { Enabled = true, SessionId = _id },
                SharedClientTelemetry.Batch _ => new SharedClientTelemetry.BatchResult { Enabled = true },
                SharedClientTelemetry.SessionEnd _ => EmptyResponse.Ok,
                _ => throw new InvalidOperationException($"Unexpected {request.GetType().Name}")
            };
        }

        private static TelemetryFixture Fixture(FakeTelemetrySocket socket)
        {
            var fixture = new TelemetryFixture(transport: new TelemetrySocketTransport(socket));
            fixture.Collector.Script(sample => sample.Gauge("a", 1));
            return fixture;
        }

        [UnityTest]
        public IEnumerator Session_StartThenBatchesThenEnd() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter(Enabled);
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => writer.Requests.OfType<SharedClientTelemetry.Batch>().Any());
            lifetime.Terminate();
            await Wait(() => writer.Requests.OfType<SharedClientTelemetry.SessionEnd>().Any());

            var requests = writer.Requests;
            Assert.IsInstanceOf<SharedClientTelemetry.SessionStart>(requests[0]);
            Assert.IsInstanceOf<SharedClientTelemetry.SessionEnd>(requests[requests.Count - 1]);
            Assert.AreEqual(1, requests.OfType<SharedClientTelemetry.SessionStart>().Count());
            Assert.AreEqual(1, requests.OfType<SharedClientTelemetry.SessionEnd>().Count());
            Assert.AreEqual(requests.Count - 2, requests.OfType<SharedClientTelemetry.Batch>().Count());
            Assert.IsTrue(requests.OfType<SharedClientTelemetry.Batch>().All(batch => batch.SessionId == _id));
            Assert.AreEqual(_id, ((SharedClientTelemetry.SessionEnd)requests[requests.Count - 1]).SessionId);
            Assert.AreEqual("a", requests.OfType<SharedClientTelemetry.Batch>().First().Names[0].Name);
        });

        // One in flight: the next batch goes after the answer.
        [UnityTest]
        public IEnumerator Session_OneRequestInFlight() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter();
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => writer.InFlight == 1);
            writer.Answer(new SharedClientTelemetry.SessionStarted { Enabled = true, SessionId = _id });
            await Wait(() => writer.Requests.Count == 2);

            await UniTask.Delay(100, DelayType.Realtime);

            Assert.AreEqual(2, writer.Requests.Count);
            Assert.AreEqual(1, writer.InFlight);
            writer.Lose();
            lifetime.Terminate();
        });

        // The flag is off: the start is the only request, the next session asks again.
        [UnityTest]
        public IEnumerator FlagOff_OnlyTheStart_NextSessionAsksAgain() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter(_ => new SharedClientTelemetry.SessionStarted { Enabled = false });
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await UniTask.Delay(100, DelayType.Realtime);
            Assert.AreEqual(1, writer.Requests.Count);

            fixture.Telemetry.StartSession(lifetime, "run");
            await Wait(() => writer.Requests.Count == 2);
            await UniTask.Delay(100, DelayType.Realtime);

            Assert.AreEqual(2, writer.Requests.Count);
            Assert.IsTrue(writer.Requests.All(request => request is SharedClientTelemetry.SessionStart));
            lifetime.Terminate();
        });

        // The server stops a live session: no batch and no end after it.
        [UnityTest]
        public IEnumerator BatchNotEnabled_NothingMore() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter(request => request is SharedClientTelemetry.Batch
                ? new SharedClientTelemetry.BatchResult { Enabled = false }
                : Enabled(request));
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => writer.Requests.Count == 2);
            await UniTask.Delay(100, DelayType.Realtime);
            lifetime.Terminate();
            await UniTask.Delay(100, DelayType.Realtime);

            Assert.AreEqual(2, writer.Requests.Count);
        });

        [UnityTest]
        public IEnumerator ClosedConnection_NothingWritten_NextSessionOnAnOpenOneWorks() => UniTask.ToCoroutine(async () =>
        {
            var closed = new ScriptedTelemetryWriter(Enabled);
            closed.Lose();
            var socket = new FakeTelemetrySocket(closed);
            var fixture = Fixture(socket);
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await UniTask.Delay(100, DelayType.Realtime);
            Assert.IsEmpty(closed.Requests);

            var open = new ScriptedTelemetryWriter(Enabled);
            socket.Connect(open);
            fixture.Telemetry.StartSession(lifetime, "run");
            await Wait(() => open.Requests.OfType<SharedClientTelemetry.Batch>().Any());

            lifetime.Terminate();
        });

        // A hanging start would hold every next session back: the loss ends it and the next one goes.
        [UnityTest]
        public IEnumerator LostDuringTheStart_NextSessionGoes() => UniTask.ToCoroutine(async () =>
        {
            var lost = new ScriptedTelemetryWriter();
            var socket = new FakeTelemetrySocket(lost);
            var fixture = Fixture(socket);
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => lost.InFlight == 1);

            var open = new ScriptedTelemetryWriter(Enabled);
            socket.Connect(open);
            fixture.Telemetry.StartSession(lifetime, "run");
            await UniTask.Delay(50, DelayType.Realtime);
            Assert.IsEmpty(open.Requests);

            lost.Lose();
            await Wait(() => open.Requests.OfType<SharedClientTelemetry.SessionStart>().Any());

            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator LostDuringABatch_NoEnd_NoMore() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter(request => request is SharedClientTelemetry.Batch ? null : Enabled(request));
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => writer.InFlight == 1);
            writer.Lose();
            await UniTask.Delay(100, DelayType.Realtime);
            lifetime.Terminate();
            await UniTask.Delay(100, DelayType.Realtime);

            Assert.AreEqual(2, writer.Requests.Count);
        });

        // The timeout of the writer ends the wait of a silent server.
        [UnityTest]
        public IEnumerator TimeoutOfABatch_NextSessionGoes() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter(request => request is SharedClientTelemetry.Batch ? null : Enabled(request));
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await Wait(() => writer.InFlight == 1);
            fixture.Telemetry.StartSession(lifetime, "run");
            await UniTask.Delay(50, DelayType.Realtime);
            Assert.AreEqual(1, writer.Requests.OfType<SharedClientTelemetry.SessionStart>().Count());

            writer.Fail(new TimeoutException("No response in 20 s"));
            await Wait(() => writer.Requests.OfType<SharedClientTelemetry.SessionStart>().Count() == 2);

            writer.Lose();
            lifetime.Terminate();
        });

        // An older meta: telemetry is gone for the session, the game does not notice. An error in the log fails the test.
        [UnityTest]
        public IEnumerator UnknownCommand_NoLine_NothingMore() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ScriptedTelemetryWriter(ErrorResponse.UnknownCommand);
            var fixture = Fixture(new FakeTelemetrySocket(writer));
            var lifetime = new Lifetime();

            fixture.Telemetry.StartSession(lifetime, "menu");
            await UniTask.Delay(100, DelayType.Realtime);
            lifetime.Terminate();
            await UniTask.Delay(100, DelayType.Realtime);

            Assert.AreEqual(1, writer.Requests.Count);
            LogAssert.NoUnexpectedReceived();
        });
    }
}
