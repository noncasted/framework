using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Shared;

namespace Internal
{
    public interface IConnectionWriter
    {
        // No request is answered any more: the connection is lost (reported once, by INetworkConnection.Closed) or
        // the lifetime it was run with has ended (its owner is unloaded). Requests fail with OperationCanceledException.
        // NOT closed: a live connection after ForceSendAll; a request made there is a mistake of the caller.
        bool IsClosed { get; }

        void WriteOneWay(INetworkContext value);

        // Never returns null. The response T, or:
        // NetworkRequestException - the server answered with ErrorResponse (its command threw or is unknown);
        // TimeoutException - no response in RequestTimeoutSeconds;
        // OperationCanceledException - the connection is closed (IsClosed) before the response;
        // InvalidOperationException - a response of another type, or a request after ForceSendAll.
        UniTask<T> WriteRequest<T>(INetworkContext commandContext);
        void WriteResponse(INetworkContext commandContext, int requestId);
        void OnRequestHandled(INetworkContext commandContext, int requestId);
        UniTask ForceSendAll();
    }

    public class ConnectionWriter : IConnectionWriter
    {
        public ConnectionWriter(NetworkTraffic traffic)
        {
            _traffic = traffic;
        }

        private readonly NetworkTraffic _traffic;

        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _pendingRequests = new();

        private const float RequestTimeoutSeconds = 20f;

        private int _requestCounter;
        private IReadOnlyLifetime _connectionLifetime;
        private ILifetime _lifetime;

        // The state of one Run. The loop of a lost run may still be inside a send of its socket when the writer runs
        // again (a socket resumes on a later frame): it keeps its own run and stops there, instead of sending the rest
        // of its batch on the new socket or clearing a batch the new loop enumerates.
        private RunState _run;

        // Never run: no connection to answer either.
        public bool IsClosed => _run == null || _run.IsClosed == true;

        public void Run(IReadOnlyLifetime lifetime, IWebSocket webSocket)
        {
            if (_connectionLifetime != null && _connectionLifetime.IsTerminated == false)
                throw new InvalidOperationException("[Network] The writer runs again while its previous connection is alive");

            // A connection may be run again after a loss (the meta socket): the state of the previous one goes.
            var run = new RunState(lifetime, webSocket);
            _run = run;
            _connectionLifetime = lifetime;

            // Ended by ForceSendAll as well, while the connection itself is still alive.
            _lifetime = lifetime.Child();

            webSocket.Closed.Advise(lifetime, OnClosed);

            Loop(run).Forget();
        }

        /// <summary>
        /// Connection loss: no more responses will arrive, so pending requests complete
        /// immediately instead of on timeout. The send queue stops, there is no socket to write to.
        /// </summary>
        private void OnClosed(SocketCloseInfo info)
        {
            Close();
        }

        /// <summary>
        /// The socket of this run never opened (NetworkConnection.Run: its Connect failed): the connection is closed
        /// as after a loss, so a caller that checks <see cref="IsClosed"/> opens it again instead of taking it alive.
        /// </summary>
        public void Close()
        {
            _run.IsLost = true;
            _run.Queue.Clear();

            // Copy: TrySetCanceled synchronously wakes the waiters, and they remove themselves from the dictionary.
            var pending = new List<UniTaskCompletionSource<INetworkContext>>(_pendingRequests.Values);
            _pendingRequests.Clear();

            foreach (var completion in pending)
                completion.TrySetCanceled();
        }

        // The only sender of a run: ForceSendAll waits for it instead of sending by itself, a socket takes one send at
        // a time (ClientWebSocket throws on a second outstanding SendAsync).
        private async UniTask Loop(RunState run)
        {
            try
            {
                while (run.IsClosed == false)
                {
                    if (run.Queue.Count == 0)
                    {
                        // Everything written before ForceSendAll is sent.
                        run.CompleteFlushes();

                        // Every frame: a 50 ms sleep added up to three frames of latency to each packet.
                        await UniTask.Yield();
                        continue;
                    }

                    // The whole queue is flushed per frame: with one message per frame it grew faster than
                    // it drained (each dirty property is a separate message), and in WebGL the latency
                    // built up to seconds.
                    var batch = new List<IMessageFromClient>(run.Queue);
                    run.Queue.Clear();
                    _traffic.OnBatch(batch.Count);

                    foreach (var message in batch)
                    {
                        if (run.IsClosed == true)
                            break;

                        await Send(run, message);
                    }
                }
            }
            finally
            {
                // Closed: nothing more is sent on this run, the loss itself is reported by Closed.
                run.CompleteFlushes();
            }
        }

        private async UniTask Send(RunState run, IMessageFromClient message)
        {
            byte[] payload;

            try
            {
                payload = MemoryPackSerializer.Serialize(message);
            }
            catch (Exception exception)
            {
                // A message the protocol does not know (a type missing from the union) is a mistake of the caller:
                // its request fails with the error at once instead of on the timeout, the next messages go on.
                if (message is RequestMessageFromClient request
                    && _pendingRequests.TryGetValue(request.RequestId, out var completion) == true)
                {
                    completion.TrySetException(exception);
                    return;
                }

                UnityEngine.Debug.LogException(exception);
                return;
            }

            try
            {
                await run.Socket.Send(payload);
                _traffic.OnSent(payload.Length);
            }
            catch (Exception) when (run.IsClosed == true)
            {
                // The connection is closed during the send (the lifetime ended: OperationCanceledException, the socket
                // is disposed: ObjectDisposedException): the message goes as every other one left in the queue of a
                // closed connection, not a failure to report.
            }
            catch (Exception exception)
            {
                // A crashed send loop silently hangs all subsequent requests, so we keep going.
                UnityEngine.Debug.LogError($"Failed to send message to server: {exception}");
            }
        }

        public void WriteOneWay(INetworkContext value)
        {
            var oneWay = new OneWayMessageFromClient()
            {
                Context = value,
            };

            Enqueue(oneWay);
        }

        public async UniTask<T> WriteRequest<T>(INetworkContext commandContext)
        {
            var name = ErrorResponse.Describe(commandContext);

            // Nobody answers on a closed connection: without this the request would wait for its timeout, and
            // a chain of requests (entering a session) - for one timeout per request.
            if (IsClosed == true)
                throw new OperationCanceledException($"Connection is closed: request {name} is not sent");

            if (_lifetime.IsTerminated == true)
                throw new InvalidOperationException($"Request {name} after ForceSendAll");

            // The request goes to the queue and awaits the response in parallel with anything else.
            using var trace = GameProfiler.Concurrent($"Request: {name}");

            _requestCounter++;

            var request = new RequestMessageFromClient()
            {
                Context = commandContext,
                RequestId = _requestCounter
            };

            var completion = new UniTaskCompletionSource<INetworkContext>();
            _pendingRequests.Add(request.RequestId, completion);

            Enqueue(request);

            // The end of the connection (or ForceSendAll) cancels the request. Removed when the request ends:
            // a listener per request would otherwise stay on the connection lifetime until it ends.
            _lifetime.Listen(OnLifetimeEnded);

            // The response may never arrive: the server handler crashed, the packet was lost. Cancelled by the response.
            var timeout = new CancellationTokenSource();
            LimitTime().Forget();

            INetworkContext context;

            try
            {
                context = await completion.Task;
            }
            finally
            {
                _pendingRequests.Remove(request.RequestId);
                _lifetime.RemoveListener(OnLifetimeEnded);
                timeout.Cancel();
                timeout.Dispose();
            }

            return context switch
            {
                T typed => typed,
                ErrorResponse error => throw new NetworkRequestException(error.Code, error.Message, name),
                _ => throw new InvalidOperationException(
                    $"Request {name} got {context.GetType().Name}, expected {typeof(T).Name}")
            };

            void OnLifetimeEnded()
            {
                completion.TrySetCanceled();
            }

            async UniTaskVoid LimitTime()
            {
                var isCanceled = await UniTask
                                       .Delay(TimeSpan.FromSeconds(RequestTimeoutSeconds),
                                           DelayType.Realtime,
                                           cancellationToken: timeout.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                completion.TrySetException(new TimeoutException(
                    $"Request {name} got no response in {RequestTimeoutSeconds} s, expected {typeof(T).Name}"));
            }
        }

        public void WriteResponse(INetworkContext commandContext, int requestId)
        {
            var response = new ResponseMessageFromClient()
            {
                RequestId = requestId,
                Context = commandContext
            };

            Enqueue(response);
        }

        public void OnRequestHandled(INetworkContext commandContext, int requestId)
        {
            if (_pendingRequests.TryGetValue(requestId, out var pending))
            {
                pending.TrySetResult(commandContext);
                _pendingRequests.Remove(requestId);
            }
            else
            {
                // Expected case: the response arrived after a timeout or connection loss, the request is already removed.
                UnityEngine.Debug.LogWarning($"Response for unknown request {requestId} ignored");
            }
        }

        // A closed connection sends nothing: the message goes as every other one left in its queue.
        private void Enqueue(IMessageFromClient message)
        {
            if (IsClosed == true)
                return;

            _run.Queue.Add(message);
        }

        // Sends everything written so far before it returns. The send loop does it: a second sender would overlap its
        // send. Messages written after it still go with the loop; requests are refused.
        public UniTask ForceSendAll()
        {
            _lifetime.Terminate();

            if (_run.IsClosed == true)
                return UniTask.CompletedTask;

            var flush = new UniTaskCompletionSource();
            _run.Flushes.Add(flush);
            return flush.Task;
        }

        private class RunState
        {
            public RunState(IReadOnlyLifetime lifetime, IWebSocket socket)
            {
                Lifetime = lifetime;
                Socket = socket;
            }

            public readonly IReadOnlyLifetime Lifetime;
            public readonly IWebSocket Socket;
            public readonly List<IMessageFromClient> Queue = new();
            public readonly List<UniTaskCompletionSource> Flushes = new();

            public bool IsLost;

            public bool IsClosed => IsLost == true || Lifetime.IsTerminated == true;

            public void CompleteFlushes()
            {
                if (Flushes.Count == 0)
                    return;

                var flushes = new List<UniTaskCompletionSource>(Flushes);
                Flushes.Clear();

                foreach (var flush in flushes)
                    flush.TrySetResult();
            }
        }
    }
}
