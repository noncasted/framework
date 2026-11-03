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

        // A message is serialized when it is enqueued, into the buffer of the queue; the loop swaps the two buffers and
        // sends the messages of the other one while the next are written. Both are reused: a steady stream of messages
        // allocates nothing, and a message object may be reused by its writer at once.
        private MessageQueue _queue = new();
        private MessageQueue _sending = new();

        private readonly List<UniTaskCompletionSource> _flushes = new();

        private bool _isLost;

        public bool IsClosed => _isLost == true || _lifetime.IsTerminated == true;

        public void Enqueue(IMessageFromClient message)
        {
            var start = _queue.Buffer.Count;

            try
            {
                MemoryPackSerializer.Serialize(_queue.Buffer, message);
            }
            catch (Exception exception)
            {
                _queue.Buffer.Truncate(start);

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

            _queue.EndMessage(start);
        }

        // A message serialized by the caller: the bytes of an IMessageFromClient.
        public void Enqueue(ReadOnlySpan<byte> message)
        {
            var start = _queue.Buffer.Count;
            message.CopyTo(_queue.Buffer.GetSpan(message.Length));
            _queue.Buffer.Advance(message.Length);
            _queue.EndMessage(start);
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

                    await SendQueued();
                }
            }
            finally
            {
                // Closed: nothing more is sent on this run, the loss itself is reported by Closed.
                CompleteFlushes();
            }
        }

        // The whole queue is flushed per frame: with one message per frame it grew faster than it drained (each dirty
        // property was a separate message), and in WebGL the latency built up to seconds. No async method on the way of a
        // socket that sends synchronously (JsWebSocket): an editor build makes a state machine object per call.
        public UniTask SendQueued()
        {
            (_queue, _sending) = (_sending, _queue);
            _queue.Clear();
            _traffic.OnBatch(_sending.Count);

            return SendFrom(0);
        }

        private UniTask SendFrom(int index)
        {
            for (var i = index; i < _sending.Count; i++)
            {
                if (IsClosed == true)
                    break;

                var payload = _sending.Message(i);
                UniTask send;

                try
                {
                    send = _socket.Send(payload);
                }
                catch (Exception exception)
                {
                    OnSendFailed(exception);
                    continue;
                }

                if (send.Status != UniTaskStatus.Succeeded)
                    return AwaitSend(send, payload.Count, i);

                _traffic.OnSent(payload.Count);
            }

            _sending.Clear();
            return UniTask.CompletedTask;
        }

        private async UniTask AwaitSend(UniTask send, int length, int index)
        {
            try
            {
                await send;
                _traffic.OnSent(length);
            }
            catch (Exception exception)
            {
                OnSendFailed(exception);
            }

            await SendFrom(index + 1);
        }

        private void OnSendFailed(Exception exception)
        {
            // The connection is closed during the send (the lifetime ended: OperationCanceledException, the socket is
            // disposed: ObjectDisposedException): the message goes as every other one left in the queue of a closed
            // connection, not a failure to report.
            if (IsClosed == true && exception is OperationCanceledException or ObjectDisposedException)
                return;

            // A crashed send loop silently hangs all subsequent requests, so we keep going.
            UnityEngine.Debug.LogError($"Failed to send message to server: {exception}");
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

        // The serialized messages of one side of the queue, one after another in a reused buffer.
        private sealed class MessageQueue
        {
            public readonly NetworkBuffer Buffer = new();
            private readonly List<int> _starts = new();

            public int Count => _starts.Count;

            public void EndMessage(int start)
            {
                _starts.Add(start);
            }

            public ArraySegment<byte> Message(int index)
            {
                var start = _starts[index];
                var end = index + 1 < _starts.Count ? _starts[index + 1] : Buffer.Count;
                return Buffer.Segment(start, end - start);
            }

            public void Clear()
            {
                Buffer.Clear();
                _starts.Clear();
            }
        }
    }
}
