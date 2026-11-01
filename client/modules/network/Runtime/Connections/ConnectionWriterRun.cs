using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Shared;

namespace Internal
{
    // One run of a ConnectionWriter over one socket: the send queue, its loop and the waiters of a flush.
    public class ConnectionWriterRun
    {
        public ConnectionWriterRun(
            IReadOnlyLifetime lifetime,
            IWebSocket socket,
            NetworkTraffic traffic,
            PendingRequests pendingRequests)
        {
            _lifetime = lifetime;
            _socket = socket;
            _traffic = traffic;
            _pendingRequests = pendingRequests;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IWebSocket _socket;
        private readonly NetworkTraffic _traffic;
        private readonly PendingRequests _pendingRequests;

        private readonly List<IMessageFromClient> _queue = new();
        private readonly List<UniTaskCompletionSource> _flushes = new();

        private bool _isLost;

        public bool IsClosed => _isLost == true || _lifetime.IsTerminated == true;

        public void Enqueue(IMessageFromClient message)
        {
            _queue.Add(message);
        }

        public void Lose()
        {
            _isLost = true;
            _queue.Clear();
        }

        // Completes when everything enqueued so far is sent, or at once on a closed run.
        public UniTask Flush()
        {
            if (IsClosed == true)
                return UniTask.CompletedTask;

            var flush = new UniTaskCompletionSource();
            _flushes.Add(flush);
            return flush.Task;
        }

        // The only sender of a run: ForceSendAll waits for it instead of sending by itself, a socket takes one send at
        // a time (ClientWebSocket throws on a second outstanding SendAsync).
        public async UniTask SendLoop()
        {
            try
            {
                while (IsClosed == false)
                {
                    if (_queue.Count == 0)
                    {
                        // Everything written before ForceSendAll is sent.
                        CompleteFlushes();

                        // Every frame: a 50 ms sleep added up to three frames of latency to each packet.
                        await UniTask.Yield();
                        continue;
                    }

                    // The whole queue is flushed per frame: with one message per frame it grew faster than
                    // it drained (each dirty property is a separate message), and in WebGL the latency
                    // built up to seconds.
                    var batch = new List<IMessageFromClient>(_queue);
                    _queue.Clear();
                    _traffic.OnBatch(batch.Count);

                    foreach (var message in batch)
                    {
                        if (IsClosed == true)
                            break;

                        await Send(message);
                    }
                }
            }
            finally
            {
                // Closed: nothing more is sent on this run, the loss itself is reported by Closed.
                CompleteFlushes();
            }
        }

        private async UniTask Send(IMessageFromClient message)
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
                    && _pendingRequests.TryFail(request.RequestId, exception) == true)
                {
                    return;
                }

                UnityEngine.Debug.LogException(exception);
                return;
            }

            try
            {
                await _socket.Send(payload);
                _traffic.OnSent(payload.Length);
            }
            catch (Exception exception) when (IsClosed == true && exception is OperationCanceledException or ObjectDisposedException)
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

        private void CompleteFlushes()
        {
            if (_flushes.Count == 0)
                return;

            var flushes = new List<UniTaskCompletionSource>(_flushes);
            _flushes.Clear();

            foreach (var flush in flushes)
                flush.TrySetResult();
        }
    }
}
