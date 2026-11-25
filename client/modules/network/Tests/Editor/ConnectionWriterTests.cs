using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // ConnectionWriter: the send loop and ForceSendAll send a message once; the writer runs again after a loss.
    // WriteRequest never returns null - the typed response,
    // NetworkRequestException for ErrorResponse, InvalidOperationException for a response of another type or a request
    // after ForceSendAll, OperationCanceledException at once (not after the 20 s timeout) when the connection is lost
    // or its lifetime ends. The 20 s timeout is not covered (real time).
    public class ConnectionWriterTests
    {
        // The send loop picks the queue up within a few frames: shows the loop runs in the test, so the check of
        // ForceSendAll below is real.
        [UnityTest]
        public IEnumerator Loop_SendsQueuedMessage() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Until(() => socket.Sent.Count > 0);

            Assert.AreEqual(1, socket.Sent.Count);
            lifetime.Terminate();
        });

        // ForceSendAll takes the queue: the send loop does not send the same messages a second time.
        [UnityTest]
        public IEnumerator ForceSendAll_LoopDoesNotSendQueueAgain() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await writer.ForceSendAll();
            await LoopTurns();

            Assert.AreEqual(1, socket.Sent.Count);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator SocketClosed_PendingAndNextRequestsAreCancelledAtOnce() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            var pending = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            socket.ClosedSource.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, "lost"));

            Assert.IsInstanceOf<OperationCanceledException>(await Catch(pending));
            Assert.IsTrue(writer.IsClosed);

            var next = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());

            Assert.IsTrue(next.Status.IsCompleted());
            Assert.IsInstanceOf<OperationCanceledException>(await Catch(next));
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator LifetimeEnds_PendingRequestIsCancelled() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var pending = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            lifetime.Terminate();

            Assert.IsInstanceOf<OperationCanceledException>(await Catch(pending));
        });

        [UnityTest]
        public IEnumerator Request_ErrorResponse_ThrowsNetworkRequestException() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new ErrorResponse { Code = ErrorCode.Forbidden, Message = "no" }, 1);

            var exception = await Catch(request);

            Assert.IsInstanceOf<NetworkRequestException>(exception);
            Assert.AreEqual(ErrorCode.Forbidden, ((NetworkRequestException)exception).Code);
            Assert.AreEqual("SharedSessionAuth.Request", ((NetworkRequestException)exception).RequestName);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator Request_OtherResponseType_ThrowsInvalidOperation() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new EmptyResponse(), 1);

            Assert.IsInstanceOf<InvalidOperationException>(await Catch(request));
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator ForceSendAll_ThenRequestThrowsInvalidOperation() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            await writer.ForceSendAll();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            Assert.IsInstanceOf<InvalidOperationException>(await Catch(request));
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator Request_Answered_ReturnsResponse() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new SharedSessionAuth.Response { Status = SessionAuthStatus.NoReservation }, 1);

            Assert.AreEqual(SessionAuthStatus.NoReservation, (await request).Status);
            lifetime.Terminate();
        });

        [Test]
        public void NeverRun_IsClosed()
        {
            Assert.IsTrue(new ConnectionWriter(new NetworkTraffic()).IsClosed);
        }

        // The meta reconnect runs the same writer on a new socket: the closed state of the lost one does not stay.
        [UnityTest]
        public IEnumerator RunAgainAfterLoss_SendsToNewSocket() => UniTask.ToCoroutine(async () =>
        {
            var (writer, lostSocket, lostLifetime) = Create();

            lostSocket.ClosedSource.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, "lost"));
            Assert.IsTrue(writer.IsClosed);
            lostLifetime.Terminate();

            var socket = new FakeSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);

            Assert.IsFalse(writer.IsClosed);

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Until(() => socket.Sent.Count > 0);

            Assert.AreEqual(1, socket.Sent.Count);
            Assert.AreEqual(0, lostSocket.Sent.Count);
            lifetime.Terminate();
        });

        // The loop of a lost run may still be inside a send of its socket when the writer runs again: the rest of its
        // batch is not sent on the new socket, and the new loop does not die on a batch shared with the old one.
        [UnityTest]
        public IEnumerator RunAgainWhileLostSendPending_NewSocketGetsOnlyItsMessages() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var lostSocket = new ManualSocket();
            var lostLifetime = new Lifetime();
            writer.Run(lostLifetime, lostSocket);

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Until(() => lostSocket.Started > 0);

            Assert.AreEqual(1, lostSocket.Started);

            lostSocket.Drop("lost");
            lostLifetime.Terminate();

            var socket = new FakeSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.NoReservation });
            await Until(() => socket.Sent.Count > 0);

            // The lost loop goes on from here, before the turn of the new loop that sends the next message.
            lostSocket.CompleteAll();

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.NoReservation });
            await Until(() => socket.Sent.Count > 1);

            Assert.AreEqual(1, lostSocket.Started);
            Assert.AreEqual(2, socket.Sent.Count);
            lifetime.Terminate();
        });

        // ForceSendAll while the loop is inside a send: the sends do not overlap (ClientWebSocket allows one outstanding
        // SendAsync), the queued messages go after the one in flight, and they are counted as traffic.
        [UnityTest]
        public IEnumerator ForceSendAll_WhileLoopSends_SendsDoNotOverlap() => UniTask.ToCoroutine(async () =>
        {
            var traffic = new NetworkTraffic();
            var writer = new ConnectionWriter(traffic);
            var socket = new ManualSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Until(() => socket.Started > 0);

            Assert.AreEqual(1, socket.Started);

            writer.WriteOneWay(new SharedSessionPlayer.Leave());
            var force = writer.ForceSendAll();

            await LoopTurns();
            Assert.AreEqual(1, socket.Started, "a send started while another one is outstanding");

            socket.CompleteAll();
            await Until(() => socket.Started > 1);
            socket.CompleteAll();

            await force.Timeout(TimeSpan.FromSeconds(2));

            Assert.AreEqual(2, socket.Started);
            Assert.AreEqual(0, socket.Overlaps);
            Assert.AreEqual(2, traffic.SentMessages);
            lifetime.Terminate();
        });

        // A request that fails to serialize is failed at once with the error, not left to its 20 s timeout.
        [UnityTest]
        public IEnumerator Request_NotSerializable_FailsAtOnce() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<EmptyResponse>(new UnknownContext());
            var exception = await Catch(request.Timeout(TimeSpan.FromSeconds(2)));

            Assert.IsInstanceOf<MemoryPack.MemoryPackSerializationException>(exception, exception.ToString());
            lifetime.Terminate();
        });

        // A connection closed locally while the loop sends: the socket refuses the rest (disposed), and that is not
        // reported as a failed send.
        [UnityTest]
        public IEnumerator LifetimeEndsDuringSend_RestIsNotSentNorLogged() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var socket = new ManualSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Until(() => socket.Started > 0);

            lifetime.Terminate();
            socket.Dispose();
            await LoopTurns();

            Assert.AreEqual(1, socket.Started);
            LogAssert.NoUnexpectedReceived();
        });

        [Test]
        public void RunAgainWhileAlive_Throws()
        {
            var (writer, _, lifetime) = Create();

            Assert.Throws<InvalidOperationException>(() => writer.Run(lifetime, new FakeSocket()));
            lifetime.Terminate();
        }

        private static async UniTask<Exception> Catch<T>(UniTask<T> task)
        {
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("The request was expected to fail");
            return null;
        }

        [Test]
        public void Response_ForUnknownRequest_IsIgnoredWithWarning()
        {
            var (writer, _, lifetime) = Create();

            LogAssert.Expect(LogType.Warning, "[ConnectionWriter] Response for unknown request 42 ignored");
            writer.OnRequestHandled(new SharedSessionLatency.Pong(), 42);

            lifetime.Terminate();
        }

        private static (ConnectionWriter, FakeSocket, Lifetime) Create()
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var socket = new FakeSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);
            return (writer, socket, lifetime);
        }

        // What must happen: the wait ends with it, a loaded machine only makes it longer.
        private static async UniTask Until(Func<bool> condition)
        {
            await UniTask.WaitUntil(condition).Timeout(TimeSpan.FromSeconds(10));
        }

        // What must not happen has no event to wait for: the idle send loop checks the queue every frame, so a few
        // frames are a few turns of it, on a loaded machine too.
        private static UniTask LoopTurns()
        {
            return UniTask.DelayFrame(5);
        }

        // Only the failures of a closing connection are quiet: any other one is a mistake, also while the connection closes.
        [UnityTest]
        public IEnumerator SendFails_WhileClosing_OtherErrorIsLogged() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var lifetime = new Lifetime();
            writer.Run(lifetime, new ThrowingSocket(lifetime));

            LogAssert.Expect(LogType.Error, "[ConnectionWriterRun] Failed to send message to server");
            LogAssert.Expect(LogType.Exception, new Regex("the encoder broke"));
            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Until(() => lifetime.IsTerminated);
        });

        // The browser peer exists only in a WebGL player: its __Internal imports do not link anywhere else.
        [Test]
        public void JsWebRtcPeer_IsNotCompiledOutsideOfWebGL()
        {
            Assert.IsNull(typeof(IWebRtcPeer).Assembly.GetType("Internal.JsWebRtcPeer"));
        }

        private class UnknownContext : INetworkContext
        {
        }

        // A send completes when the test says so, as a ClientWebSocket send waits for the network; a send started while
        // another one is outstanding is counted (ClientWebSocket throws there). Disposed, it fails every send as the
        // disposed ClientWebSocket of a terminated DefaultWebSocket does.
        private class ManualSocket : IWebSocket
        {
            private readonly ViewableDelegate<SocketCloseInfo> _closed = new();
            private readonly List<UniTaskCompletionSource> _pending = new();

            private bool _isDisposed;

            public int Started { get; private set; }
            public int Overlaps { get; private set; }

            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed => _closed;

            public UniTask Connect()
            {
                return UniTask.CompletedTask;
            }

            public UniTask Send(ArraySegment<byte> bytes)
            {
                if (_isDisposed == true)
                    throw new ObjectDisposedException(nameof(ManualSocket));

                if (_pending.Count > 0)
                    Overlaps++;

                Started++;

                var completion = new UniTaskCompletionSource();
                _pending.Add(completion);
                return completion.Task;
            }

            public void CompleteAll()
            {
                var pending = new List<UniTaskCompletionSource>(_pending);
                _pending.Clear();

                foreach (var completion in pending)
                    completion.TrySetResult();
            }

            public void Dispose()
            {
                _isDisposed = true;

                var pending = new List<UniTaskCompletionSource>(_pending);
                _pending.Clear();

                foreach (var completion in pending)
                    completion.TrySetException(new ObjectDisposedException(nameof(ManualSocket)));
            }

            public void Drop(string reason)
            {
                _closed.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, reason));
            }
        }

        private class ThrowingSocket : IWebSocket
        {
            public ThrowingSocket(Lifetime lifetime)
            {
                _lifetime = lifetime;
            }

            private readonly Lifetime _lifetime;

            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();

            public UniTask Connect()
            {
                return UniTask.CompletedTask;
            }

            public UniTask Send(ArraySegment<byte> bytes)
            {
                _lifetime.Terminate();
                throw new InvalidOperationException("the encoder broke");
            }

            public void Drop(string reason)
            {
            }
        }

        private class FakeSocket : IWebSocket
        {
            public readonly ViewableDelegate<SocketCloseInfo> ClosedSource = new();
            public readonly List<byte[]> Sent = new();

            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed => ClosedSource;

            public UniTask Connect()
            {
                return UniTask.CompletedTask;
            }

            public UniTask Send(ArraySegment<byte> bytes)
            {
                Sent.Add(bytes.ToArray());
                return UniTask.CompletedTask;
            }

            public void Drop(string reason)
            {
                ClosedSource.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, reason));
            }
        }
    }
}
