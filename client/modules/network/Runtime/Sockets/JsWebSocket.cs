using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// WebSocket implementation for WebGL platform using JavaScript interop.
    /// Uses handler-based architecture with unique IDs for each connection.
    /// </summary>
    [AutoStaticsCleanup]
    public partial class JsWebSocket : IWebSocket
    {
        // Static dictionary to map handler IDs to C# instances for callbacks
        private static readonly Dictionary<int, JsWebSocket> Instances = new();
        private static bool _callbacksInitialized;

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
        private readonly Queue<byte[]> _inbox = new();
        private SocketCloseInfo? _pendingClose;

        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public JsWebSocket(string url, IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime.Child();

            // Initialize global callbacks once
            if (!_callbacksInitialized)
            {
                InitializeCallbacks();
                _callbacksInitialized = true;
            }

            // Create handler and get unique ID
            _handlerId = JsWebSocketCreate(url);

            // Register this instance for callbacks
            Instances[_handlerId] = this;

            // Setup cleanup on lifetime termination
            _lifetime.Listen(Dispose);

            Deliver().Forget();

            Debug.Log($"[Network] [JsWebSocket] Created: id={_handlerId}, url={url}");
        }

        public async UniTask Connect()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            if (_isConnected)
            {
                Debug.LogWarning($"[Network] [JsWebSocket] Already connected: id={_handlerId}");
                return;
            }

            Debug.Log($"[Network] [JsWebSocket] Connecting: id={_handlerId}");

            var result = JsWebSocketConnect(_handlerId);

            if (result != 0)
            {
                throw new Exception($"Failed to connect WebSocket: error={result}");
            }

            await WaitOpened();

            Debug.Log($"[Network] [JsWebSocket] Connected: id={_handlerId}");
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

        public UniTask Send(byte[] bytes)
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
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var result = JsWebSocketSend(_handlerId, ptr, bytes.Length);

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

            Debug.LogWarning($"[Network] [JsWebSocket] Dropped: id={_handlerId}, {reason}");
            _closed.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"dropped: {reason}"));

            JsWebSocketClose(_handlerId, 4000, "Dropped");
        }

        private void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            Debug.Log($"[Network] [JsWebSocket] Disposing: id={_handlerId}");

            // Close connection if still open
            if (_isConnected)
            {
                JsWebSocketClose(_handlerId, 1000, "Normal closure");
            }

            // Free handler resources
            JsWebSocketFree(_handlerId);

            // Remove from instances
            Instances.Remove(_handlerId);

            // Clear event listeners
            _received.Dispose();
            _closed.Dispose();

            Debug.Log($"[Network] [JsWebSocket] Disposed: id={_handlerId}");
        }

        private async UniTaskVoid Deliver()
        {
            while (true)
            {
                var isCanceled = await UniTask
                                       .Yield(PlayerLoopTiming.Update, _lifetime.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                while (_inbox.Count > 0 && _isDisposed == false)
                {
                    var data = _inbox.Dequeue();

                    try
                    {
                        _received.Invoke(data);
                    }
                    catch (Exception exception)
                    {
                        // The next frames go on, as the receive loop of DefaultWebSocket does not stop on an error of
                        // the code above it.
                        Debug.LogException(exception);
                    }
                }

                if (_pendingClose.HasValue == true && _isDisposed == false)
                {
                    var info = _pendingClose.Value;
                    _pendingClose = null;
                    _closed.Invoke(info);
                }
            }
        }

        #region JavaScript Interop

        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnOpen(Action<int> callback);

        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnMessage(Action<int, IntPtr, int> callback);

        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnError(Action<int, IntPtr> callback);

        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnClose(Action<int, int> callback);

        [DllImport("__Internal")]
        private static extern int JsWebSocketCreate(string url);

        [DllImport("__Internal")]
        private static extern int JsWebSocketConnect(int id);

        [DllImport("__Internal")]
        private static extern int JsWebSocketSend(int id, IntPtr buffer, int length);

        [DllImport("__Internal")]
        private static extern int JsWebSocketClose(int id, int code, string reason);

        [DllImport("__Internal")]
        private static extern int JsWebSocketFree(int id);

        [DllImport("__Internal")]
        private static extern int JsWebSocketGetState(int id);
#else
        private static void JsWebSocketSetOnOpen(Action<int> callback) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static void JsWebSocketSetOnMessage(Action<int, IntPtr, int> callback) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static void JsWebSocketSetOnError(Action<int, IntPtr> callback) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static void JsWebSocketSetOnClose(Action<int, int> callback) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int JsWebSocketCreate(string url) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int JsWebSocketConnect(int id) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int JsWebSocketSend(int id, IntPtr buffer, int length) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int JsWebSocketClose(int id, int code, string reason) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int JsWebSocketFree(int id) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");

        private static int JsWebSocketGetState(int id) =>
            throw new System.PlatformNotSupportedException("[Web] A function of the .jslib outside a WebGL player");
#endif

        #endregion

        #region Callbacks (must be static for AOT)

        private static void InitializeCallbacks()
        {
            JsWebSocketSetOnOpen(OnOpen);
            JsWebSocketSetOnMessage(OnMessage);
            JsWebSocketSetOnError(OnError);
            JsWebSocketSetOnClose(OnClose);

            Debug.Log("[Network] [JsWebSocket] Global callbacks initialized");
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnOpen(int handlerId)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                Debug.Log($"[Network] [JsWebSocket] OnOpen: id={handlerId}");
                instance._isConnected = true;
                instance._isOpened = true;
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnOpen: instance not found for id={handlerId}");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        private static void OnMessage(int handlerId, IntPtr bufferPtr, int length)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                // Copy data from unmanaged memory to managed byte array: the buffer is freed after the callback.
                var data = new byte[length];
                Marshal.Copy(bufferPtr, data, 0, length);

                instance._inbox.Enqueue(data);
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnMessage: instance not found for id={handlerId}");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr>))]
        private static void OnError(int handlerId, IntPtr errorPtr)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                var errorMessage = Marshal.PtrToStringAnsi(errorPtr);
                Debug.LogError($"[Network] [JsWebSocket] OnError: id={handlerId}, error={errorMessage}");

                // The connection state is left to OnClose, which the browser always sends after onerror: clearing
                // _isConnected here made OnClose take an open socket for a never opened one and swallow the loss.
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnError: instance not found for id={handlerId}");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnClose(int handlerId, int code)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                Debug.Log($"[Network] [JsWebSocket] OnClose: id={handlerId}, code={code}");

                var wasConnected = instance._isConnected;
                instance._isConnected = false;
                instance._isClosed = true;
                instance._closeCode = code;

                // The browser sends onclose after onerror too, so the loss is reported only from here.
                // Dispose removes the instance from Instances before JsWebSocketClose, so a local
                // close does not get here.
                // Reported after the frames received before it, in the player loop.
                if (wasConnected == true && instance._isDisposed == false)
                    instance._pendingClose = new SocketCloseInfo(code, "closed by browser");
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnClose: instance not found for id={handlerId}");
            }
        }

        #endregion
    }
}