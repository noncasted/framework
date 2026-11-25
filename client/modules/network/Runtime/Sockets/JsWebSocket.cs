using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;

namespace Internal
{
    /// <summary>
    /// WebSocket implementation for WebGL platform using JavaScript interop.
    /// Uses handler-based architecture with unique IDs for each connection.
    /// </summary>
    public class JsWebSocket : IWebSocket
    {
        private static readonly ILog _log = Logs.Of<JsWebSocket>();

        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();
        private readonly IReadOnlyLifetime _lifetime;
        private readonly int _handlerId;

        // JsWebSocketSend: readyState is not OPEN.
        private const int SendNotOpen = -3;

        private bool _isConnected;
        private bool _isOpened;
        private bool _isDisposed;

        // Closed by the browser: before onopen (the server refused or is gone) or right after it (a server that
        // accepts and closes at once, ProtocolVersion.ClientOutdatedCloseCode) - possibly within one frame.
        private bool _isClosed;
        private int _closeCode;

        // The browser calls OnMessage and OnClose from its own events, outside the player loop: the frames and the
        // loss are handed to the listeners in the player loop, in their order, as DefaultWebSocket and
        // LoopbackWebSocket do. Run inside the browser event, the whole command pipeline ran between two frames of
        // Unity (and its exceptions reached the browser).
        private readonly SocketInbox<byte[]> _inbox;
        private SocketCloseInfo? _pendingClose;

        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public JsWebSocket(string url, IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime.Child();
            _inbox = new SocketInbox<byte[]>(_lifetime, bytes => _received.Invoke(bytes));

            // Initialize global callbacks once
            JsWebSocketCallbacks.Initialize();

            // Create handler and get unique ID
            _handlerId = JsWebSocketInterop.JsWebSocketCreate(url);

            // Register this instance for callbacks
            JsWebSocketCallbacks.Register(_handlerId, this);

            // Setup cleanup on lifetime termination
            _lifetime.Listen(Dispose);

            SocketPump.Run(_lifetime, Deliver).NoAwait();

            _log.Debug("Created: id={Id}, url={Url}", _handlerId, url);
        }

        public async UniTask Connect()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            if (_isConnected)
            {
                _log.Warning("Already connected: id={Id}", _handlerId);
                return;
            }

            _log.Debug("Connecting: id={Id}", _handlerId);

            var result = JsWebSocketInterop.JsWebSocketConnect(_handlerId);

            if (result != 0)
            {
                throw new Exception($"Failed to connect WebSocket: error={result}");
            }

            await WaitOpened();

            _log.Info("Connected: id={Id}", _handlerId);
        }

        private async UniTask WaitOpened()
        {
            // Wait for connection to be established
            await UniTask.WaitUntil(() => _isConnected || _isDisposed || _isClosed,
                cancellationToken: _lifetime.Token);

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            // Without this the wait never ended for a socket closed before the next frame. A socket that did open
            // connects: its close, with the code of the server, comes through Closed.
            if (_isClosed == true && _isOpened == false)
                throw new WebSocketClosedException(_closeCode);
        }

        public UniTask Send(ArraySegment<byte> bytes)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            // Closed by the browser: the loss is already reported by OnClose with the code of the server, the message
            // is dropped as every other one left in the queue of a lost connection.
            if (_isClosed == true)
                return UniTask.CompletedTask;

            if (!_isConnected)
                throw new InvalidOperationException("WebSocket is not connected");

            // Pin the byte array and get pointer for JS interop
            var handle = GCHandle.Alloc(bytes.Array, GCHandleType.Pinned);

            try
            {
                var ptr = handle.AddrOfPinnedObject() + bytes.Offset;
                var result = JsWebSocketInterop.JsWebSocketSend(_handlerId, ptr, bytes.Count);

                // CLOSING: the server Close frame has arrived, onclose with its code follows. Reporting the loss
                // from the send would race it, as the 1006 of DefaultWebSocket.Send hid the 4001 of an outdated client.
                if (result == SendNotOpen)
                    return UniTask.CompletedTask;

                if (result != 0)
                    throw new Exception($"Failed to send data: error={result}");
            }
            finally
            {
                handle.Free();
            }

            // ws.send does not block, the browser buffers by itself. Awaiting a frame here limited
            // sending to one message per frame.
            return UniTask.CompletedTask;
        }

        public void Drop(string reason)
        {
            if (_isDisposed == true || _isClosed == true || _isConnected == false)
                return;

            // OnClose of this close does not report it again: the socket is not connected any more.
            _isConnected = false;
            _isClosed = true;
            _closeCode = SocketCloseInfo.AbnormalClosure;

            _log.Warning("Dropped: id={Id}, {Reason}", _handlerId, reason);
            _closed.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"dropped: {reason}"));

            JsWebSocketInterop.JsWebSocketClose(_handlerId, 4000, "Dropped");
        }

        private void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _log.Debug("Disposing: id={Id}", _handlerId);

            // Close connection if still open
            if (_isConnected)
            {
                JsWebSocketInterop.JsWebSocketClose(_handlerId, 1000, "Normal closure");
            }

            // Free handler resources
            JsWebSocketInterop.JsWebSocketFree(_handlerId);

            // Remove from instances
            JsWebSocketCallbacks.Unregister(_handlerId);

            // Clear event listeners
            _received.Dispose();
            _closed.Dispose();

            _log.Debug("Disposed: id={Id}", _handlerId);
        }

        private void Deliver()
        {
            _inbox.Drain();

            if (_pendingClose.HasValue == true && _isDisposed == false)
            {
                var info = _pendingClose.Value;
                _pendingClose = null;
                _closed.Invoke(info);
            }
        }

        // The socket is open in the browser: OnOpen of JsWebSocketCallbacks.
        internal void OnBrowserOpen()
        {
            _isConnected = true;
            _isOpened = true;
        }

        // A frame from the browser: OnMessage of JsWebSocketCallbacks.
        internal void OnBrowserMessage(byte[] data)
        {
            _inbox.Enqueue(data);
        }

        // The socket is closed in the browser: OnClose of JsWebSocketCallbacks.
        internal void OnBrowserClose(int code)
        {
            var wasConnected = _isConnected;
            _isConnected = false;
            _isClosed = true;
            _closeCode = code;

            // The browser sends onclose after onerror too, so the loss is reported only from here.
            // Dispose removes the instance from Instances before JsWebSocketClose, so a local
            // close does not get here.
            // Reported after the frames received before it, in the player loop.
            if (wasConnected == true && _isDisposed == false)
                _pendingClose = new SocketCloseInfo(code, "closed by browser");
        }
    }
}
