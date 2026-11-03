using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // One call of the transport against one answer of the connection. Every wait is bounded: a call that does not end
    // blocks every next session, and here it fails the test.
    public class TelemetrySocketTransportTests
    {
        private static readonly Guid _id = new Guid(5, 0, 0, new byte[8]);
        private static readonly TimeSpan _limit = TimeSpan.FromSeconds(5);

        private static TelemetrySessionStart Start()
        {
            return new TelemetrySessionStart(new[] { "menu" }, new TelemetryDevice(cores: 4), Guid.Empty);
        }

        private static TelemetryBatch Batch()
        {
            return new TelemetryBatch(
                new[] { new TelemetryBatchName("a", null) },
                Array.Empty<TelemetryBatchSample>(),
                Array.Empty<TelemetryBatchMark>(),
                Array.Empty<TelemetryBatchError>());
        }

        private static TelemetrySocketTransport Transport(Func<INetworkContext, INetworkContext> server, out ScriptedTelemetryWriter writer)
        {
            writer = new ScriptedTelemetryWriter(server);
            return new TelemetrySocketTransport(new FakeTelemetrySocket(writer));
        }

        // The three calls after a failure of the same kind: each ends, the first two with Failed.
        private static async UniTask AssertAllFailed(ITelemetryTransport transport)
        {
            var answer = await transport.Start(Start()).Timeout(_limit);
            Assert.AreEqual(TelemetrySendOutcome.Failed, answer.Outcome);
            Assert.AreEqual(Guid.Empty, answer.SessionId);

            Assert.AreEqual(TelemetrySendOutcome.Failed, await transport.Send(_id, Batch()).Timeout(_limit));

            await transport.End(_id).Timeout(_limit);
        }

        // The answers.

        [UnityTest]
        public IEnumerator Start_Enabled_TheIdOfTheServer() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => new SharedClientTelemetry.SessionStarted { Enabled = true, SessionId = _id }, out var writer);

            var answer = await transport.Start(Start()).Timeout(_limit);

            Assert.AreEqual(TelemetrySendOutcome.Accepted, answer.Outcome);
            Assert.AreEqual(_id, answer.SessionId);
            var message = (SharedClientTelemetry.SessionStart)writer.Requests[0];
            CollectionAssert.AreEqual(new[] { "menu" }, message.Tags);
            Assert.AreEqual(4, message.Device.Cores);
        });

        [UnityTest]
        public IEnumerator Start_NotEnabled_Disabled() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => new SharedClientTelemetry.SessionStarted { Enabled = false }, out _);

            var answer = await transport.Start(Start()).Timeout(_limit);

            Assert.AreEqual(TelemetrySendOutcome.Disabled, answer.Outcome);
            Assert.AreEqual(Guid.Empty, answer.SessionId);
        });

        [UnityTest]
        public IEnumerator Send_Enabled_Accepted() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => new SharedClientTelemetry.BatchResult { Enabled = true }, out var writer);

            Assert.AreEqual(TelemetrySendOutcome.Accepted, await transport.Send(_id, Batch()).Timeout(_limit));

            var message = (SharedClientTelemetry.Batch)writer.Requests[0];
            Assert.AreEqual(_id, message.SessionId);
            Assert.AreEqual("a", message.Names[0].Name);
        });

        [UnityTest]
        public IEnumerator Send_NotEnabled_Disabled() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => new SharedClientTelemetry.BatchResult { Enabled = false }, out _);

            Assert.AreEqual(TelemetrySendOutcome.Disabled, await transport.Send(_id, Batch()).Timeout(_limit));
        });

        [UnityTest]
        public IEnumerator End_TheMessageOfTheSession() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => EmptyResponse.Ok, out var writer);

            await transport.End(_id).Timeout(_limit);

            Assert.AreEqual(_id, ((SharedClientTelemetry.SessionEnd)writer.Requests[0]).SessionId);
        });

        // A socket opened again is a new connection: the next call goes there.
        [UnityTest]
        public IEnumerator NewConnection_TheNextCallGoesThere() => UniTask.ToCoroutine(async () =>
        {
            var first = new ScriptedTelemetryWriter(_ => EmptyResponse.Ok);
            var second = new ScriptedTelemetryWriter(_ => EmptyResponse.Ok);
            var socket = new FakeTelemetrySocket(first);
            var transport = new TelemetrySocketTransport(socket);

            await transport.End(_id).Timeout(_limit);
            socket.Connect(second);
            await transport.End(_id).Timeout(_limit);

            Assert.AreEqual(1, first.Requests.Count);
            Assert.AreEqual(1, second.Requests.Count);
        });

        // The failures.

        [UnityTest]
        public IEnumerator Closed_NothingWritten_Failed() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => EmptyResponse.Ok, out var writer);
            writer.Lose();

            await AssertAllFailed(transport);

            Assert.IsEmpty(writer.Requests);
        });

        [UnityTest]
        public IEnumerator LostDuringTheRequest_Failed() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(null, out var writer);

            var start = transport.Start(Start());
            var send = transport.Send(_id, Batch());
            var end = transport.End(_id);
            Assert.AreEqual(3, writer.InFlight);

            writer.Lose();

            Assert.AreEqual(TelemetrySendOutcome.Failed, (await start.Timeout(_limit)).Outcome);
            Assert.AreEqual(TelemetrySendOutcome.Failed, await send.Timeout(_limit));
            await end.Timeout(_limit);
        });

        [UnityTest]
        public IEnumerator Timeout_Failed() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => throw new TimeoutException("No response in 20 s"), out _);

            await AssertAllFailed(transport);
        });

        // The timeout that comes later, while the request is in flight.
        [UnityTest]
        public IEnumerator TimeoutDuringTheRequest_Failed() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(null, out var writer);

            var send = transport.Send(_id, Batch());
            writer.Fail(new TimeoutException("No response in 20 s"));

            Assert.AreEqual(TelemetrySendOutcome.Failed, await send.Timeout(_limit));
        });

        // An older meta knows no such request: its answer is an error, not a reason to break the game. No line in the log:
        // LogAssert fails the test on an error logged.
        [UnityTest]
        public IEnumerator UnknownCommand_Failed() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(ErrorResponse.UnknownCommand, out _);

            await AssertAllFailed(transport);
        });

        [UnityTest]
        public IEnumerator HandlerOfTheServerThrew_Failed() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(
                request => ErrorResponse.Create(ErrorCode.Internal, request, new Exception("Broken")),
                out _);

            await AssertAllFailed(transport);
        });

        // Not a failure the transport expects: a protocol broken in the code (a wrong answer, a request missing from the union of
        // the game). It goes up: the session writes its one line and stops, the game goes on.
        [UnityTest]
        public IEnumerator WrongAnswer_GoesUp() => UniTask.ToCoroutine(async () =>
        {
            var transport = Transport(_ => EmptyResponse.Ok, out _);

            var caught = false;

            try
            {
                await transport.Send(_id, Batch()).Timeout(_limit);
            }
            catch (InvalidOperationException)
            {
                caught = true;
            }

            Assert.IsTrue(caught);
        });

        // The real writer. A request that reaches the socket is serialized by the union of the game, which the framework has
        // no copy of with these tags; everything here ends before the next frame, so before it is serialized.

        [UnityTest]
        public IEnumerator RealWriter_NeverRun_Failed() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var transport = new TelemetrySocketTransport(new FakeTelemetrySocket(writer));

            await AssertAllFailed(transport);
        });

        [UnityTest]
        public IEnumerator RealWriter_SocketLostDuringTheRequest_Failed() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var webSocket = new SilentWebSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, webSocket);
            var transport = new TelemetrySocketTransport(new FakeTelemetrySocket(writer));

            var send = transport.Send(_id, Batch());
            webSocket.Drop("Lost");

            Assert.AreEqual(TelemetrySendOutcome.Failed, await send.Timeout(_limit));

            await AssertAllFailed(transport);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator RealWriter_LifetimeEndsDuringTheRequest_Failed() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var lifetime = new Lifetime();
            writer.Run(lifetime, new SilentWebSocket());
            var transport = new TelemetrySocketTransport(new FakeTelemetrySocket(writer));

            var send = transport.Send(_id, Batch());
            lifetime.Terminate();

            Assert.AreEqual(TelemetrySendOutcome.Failed, await send.Timeout(_limit));

            await AssertAllFailed(transport);
        });
    }
}
