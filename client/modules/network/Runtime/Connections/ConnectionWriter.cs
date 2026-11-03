using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public class ConnectionWriter : IConnectionWriter
    {
        public ConnectionWriter(NetworkTraffic traffic)
        {
            _traffic = traffic;
        }

        private readonly NetworkTraffic _traffic;
        private readonly PendingRequests _pendingRequests = new();

        private const float RequestTimeoutSeconds = 20f;

        private int _requestCounter;
        private IReadOnlyLifetime _connectionLifetime;
        private ILifetime _lifetime;

        // The state of one Run. The loop of a lost run may still be inside a send of its socket when the writer runs
        // again (a socket resumes on a later frame): it keeps its own run and stops there, instead of sending the rest
        // of its batch on the new socket or clearing a batch the new loop enumerates.
        private ConnectionWriterRun _run;

        // Never run: no connection to answer either.
        public bool IsClosed => _run == null || _run.IsClosed == true;

        public object CurrentRun => _run;

        public void Run(IReadOnlyLifetime lifetime, IWebSocket webSocket)
        {
            if (_connectionLifetime != null && _connectionLifetime.IsTerminated == false)
                throw new InvalidOperationException("[Network] The writer runs again while its previous connection is alive");

            // A connection may be run again after a loss (the meta socket): the state of the previous one goes.
            var run = new ConnectionWriterRun(lifetime, webSocket, _traffic, _pendingRequests);
            _run = run;
            _connectionLifetime = lifetime;

            // Ended by ForceSendAll as well, while the connection itself is still alive.
            _lifetime = lifetime.Child();

            webSocket.Closed.Advise(lifetime, OnClosed);

            run.SendLoop().NoAwait();
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
            _run.Lose();
            _pendingRequests.CancelAll();
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

            var completion = _pendingRequests.Add(request.RequestId);

            Enqueue(request);

            // The end of the connection (or ForceSendAll) cancels the request. Removed when the request ends:
            // a listener per request would otherwise stay on the connection lifetime until it ends.
            _lifetime.Listen(OnLifetimeEnded);

            // The response may never arrive: the server handler crashed, the packet was lost. Cancelled by the response.
            var timeout = new CancellationTokenSource();
            LimitTime().NoAwait();

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
            if (_pendingRequests.TryComplete(requestId, commandContext) == false)
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

            _run.Enqueue(message);
        }

        // Sends everything written so far before it returns. The send loop does it: a second sender would overlap its
        // send. Messages written after it still go with the loop; requests are refused.
        public UniTask ForceSendAll()
        {
            _lifetime.Terminate();

            return _run.Flush();
        }
    }
}
