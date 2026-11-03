using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public class DefaultWebSocket : IWebSocket
    {
        private readonly ClientWebSocket _socket;

        private readonly Uri _uri;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

        private readonly ILifetime _lifetime;
        private readonly CancellationToken _cancellation;

        private readonly SocketInbox<byte[]> _inbox;

        private bool _isClosedNotified;
        private bool _isSendFailed;

        // The Close frame of the server is read. Set on the main thread before the close is acknowledged: from then
        // on the socket goes to Closed, and a send of the writer (its queue is cleared only by Closed, a frame later)
        // would fail on it. Read and written on the main thread only.
        private bool _isCloseReceived;

        private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

        public WebSocketState State => _socket.State;
        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public DefaultWebSocket(string url, IReadOnlyLifetime lifetime)
        {
            _uri = new Uri(url);
            _lifetime = lifetime.Child();
            _cancellation = _lifetime.Token;
            _inbox = new SocketInbox<byte[]>(_lifetime, bytes => _received.Invoke(bytes));

            var protocol = _uri.Scheme;

            if (!protocol.Equals("ws") && !protocol.Equals("wss"))
                throw new ArgumentException("Unsupported protocol: " + protocol);

            _socket = new ClientWebSocket();

#if UNITY_EDITOR
            // A domain reload in Play skips the lifetime: the BCL socket outlives the domain, its pending
            // ReceiveAsync keeps answering the gateway pings and the server session never closes.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += _socket.Abort;
            _lifetime.Listen(() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= _socket.Abort);
#endif
        }

        public async UniTask Connect()
        {
            // Before the connect: a lifetime that ends while it runs (NetworkConnection: the connect timed out)
            // cancels it and disposes the socket, and one that ends between the connect and the switch below closes
            // the open socket. Registered after it, the socket of a cancelled connect was never disposed.
            _lifetime.Listen(() => Shutdown().NoAwait());

            await _socket.ConnectAsync(_uri, _cancellation);

            // ClientWebSocket continues on the thread pool, while everything above the socket
            // works with Unity objects.
            await UniTask.SwitchToMainThread(_cancellation);

            SocketPump.Run(_lifetime, _inbox.Drain).NoAwait();
            Receive(_lifetime).NoAwait();
        }

        public async UniTask Send(ArraySegment<byte> bytes)
        {
            // Closed by the server: the loss goes to Closed with the code of the server (ProtocolVersion.
            // ClientOutdatedCloseCode of an outdated client), the message is dropped as every other one left in the
            // queue of a lost connection - as JsWebSocket does.
            if (_isCloseReceived == true)
                return;

            var payload = new ReadOnlyMemory<byte>(bytes.Array, bytes.Offset, bytes.Count);

            try
            {
                await _socket.SendAsync(payload, WebSocketMessageType.Binary, true, _cancellation);
            }
            catch (WebSocketException exception)
            {
                // The message is not sent, as every other one left in the queue of a lost connection. The loss is
                // reported by the receive loop: it carries the code of the Close frame of the server (4001 of an
                // outdated client), while the send, racing it, sees only InvalidState. Reporting from here
                // replaced the real code with 1006.
                OnSendFailed(exception).NoAwait();
            }

            await UniTask.SwitchToMainThread();
        }

        /// <summary>
        /// The receive loop ends on any loss of the connection (a Close frame or a socket error) and reports it.
        /// The send waits for that and reports 1006 itself only if the loop stays silent: a guarantee that the loss is
        /// never swallowed, not a second source of the close code.
        /// </summary>
        private async UniTaskVoid OnSendFailed(WebSocketException exception)
        {
            await UniTask.SwitchToMainThread();

            if (_isSendFailed == true)
                return;

            _isSendFailed = true;

            var isCanceled = await UniTask
                                   .Delay(CloseTimeout, DelayType.Realtime, cancellationToken: _cancellation)
                                   .SuppressCancellationThrow();

            if (isCanceled == true)
                return;

            await NotifyClosed(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure,
                $"send failed, receive loop silent: {exception.WebSocketErrorCode} {exception.Message}"));
        }

        /// <summary>
        /// Receiving without the lifetime token: cancelling a pending ReceiveAsync puts ClientWebSocket
        /// into Aborted, and it can no longer close properly after that. The loop ends either with
        /// a Close frame or with socket Dispose from <see cref="Shutdown"/>.
        /// </summary>
        /// <para>
        /// The loop runs on the thread pool: ClientWebSocket answers the pings of the server only inside a pending
        /// ReceiveAsync, so a loop that waited for the main thread between two receives (a long frame, a scene load)
        /// left the pings unanswered and the server timed the client out. The frames go to the main thread through
        /// the inbox, in their order.
        /// </para>
        private async UniTask Receive(IReadOnlyLifetime lifetime)
        {
            await UniTask.SwitchToThreadPool();

            var buffer = new ArraySegment<byte>(new byte[8192]);
            var closeInfo = new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, "unknown");

            try
            {
                while (lifetime.IsTerminated == false)
                {
                    using var stream = new MemoryStream();

                    WebSocketReceiveResult result;

                    do
                    {
                        // Not back to the main thread: the synchronization context of Unity would post it there.
                        result = await _socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                        stream.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (result.EndOfMessage == false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // The code of the server (ProtocolVersion.ClientOutdatedCloseCode of an outdated client), 1005
                        // for a Close frame without one, as a browser reports it.
                        var code = result.CloseStatus.HasValue == true
                            ? (int)result.CloseStatus.Value
                            : SocketCloseInfo.NoStatusReceived;

                        closeInfo = new SocketCloseInfo(code,
                            $"closed by server: {result.CloseStatus} {result.CloseStatusDescription}");

                        // Sends stop before the acknowledge moves the socket to Closed: both run on the main thread,
                        // so no send starts between the two.
                        await UniTask.SwitchToMainThread(_cancellation);
                        _isCloseReceived = true;
                        await AcknowledgeClose();
                        break;
                    }

                    _inbox.Enqueue(stream.ToArray());
                }
            }
            catch (OperationCanceledException)
            {
                // The lifetime was terminated locally: this is not a connection loss.
                return;
            }
            catch (ObjectDisposedException)
            {
                // The socket was destroyed from Shutdown.
                return;
            }
            catch (WebSocketException exception)
            {
                closeInfo = new SocketCloseInfo(SocketCloseInfo.AbnormalClosure,
                    $"socket error: {exception.WebSocketErrorCode} {exception.Message}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                closeInfo = new SocketCloseInfo(SocketCloseInfo.AbnormalClosure,
                    $"receive failed: {exception.GetType().Name} {exception.Message}");
            }

            await NotifyClosed(closeInfo);
        }

        /// <summary>
        /// The server sent Close: reply with our own Close frame to complete the handshake.
        /// CloseOutputAsync does not wait for the peer frame and does not conflict with the receive loop.
        /// </summary>
        private async UniTask AcknowledgeClose()
        {
            if (_socket.State != WebSocketState.CloseReceived)
                return;

            try
            {
                using var timeout = new CancellationTokenSource(CloseTimeout);
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network] [DefaultWebSocket] Close acknowledge failed: {exception.Message}");
            }
        }

        private async UniTask NotifyClosed(SocketCloseInfo info)
        {
            await UniTask.SwitchToMainThread();

            if (_lifetime.IsTerminated == true || _isClosedNotified == true)
                return;

            // The frames the server sent before the loss reach the listeners first, as they did before the loss.
            _inbox.Drain();

            if (_lifetime.IsTerminated == true)
                return;

            _isClosedNotified = true;

            Debug.LogWarning($"[Network] [DefaultWebSocket] Connection lost: {info}");
            _closed.Invoke(info);
        }

        /// <summary>
        /// Local termination: send a Close frame if the connection is still alive, and destroy
        /// the socket. Dispose aborts the pending ReceiveAsync, the receive loop exits without notification.
        /// </summary>
        private async UniTask Shutdown()
        {
            try
            {
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    using var timeout = new CancellationTokenSource(CloseTimeout);
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network] [DefaultWebSocket] Close failed: {exception.Message}");
            }
            finally
            {
                _socket.Dispose();
                _received.Dispose();
                _closed.Dispose();
            }
        }

        public void Drop(string reason)
        {
            NotifyClosed(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"dropped: {reason}")).NoAwait();

            // The receive loop ends with an error and finds the loss already reported.
            _socket.Abort();
        }
    }
}