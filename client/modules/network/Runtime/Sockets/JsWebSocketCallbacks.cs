using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // The events of the browser: one set of static functions (must be static for AOT), each finds its socket by the
    // handler id.
    [AutoStaticsCleanup]
    internal static partial class JsWebSocketCallbacks
    {
        // Static dictionary to map handler IDs to C# instances for callbacks
        private static readonly Dictionary<int, JsWebSocket> Instances = new();
        private static bool _isInitialized;

        public static void Initialize()
        {
            if (_isInitialized == true)
                return;

            JsWebSocketInterop.JsWebSocketSetOnOpen(OnOpen);
            JsWebSocketInterop.JsWebSocketSetOnMessage(OnMessage);
            JsWebSocketInterop.JsWebSocketSetOnError(OnError);
            JsWebSocketInterop.JsWebSocketSetOnClose(OnClose);

            Debug.Log("[Network] [JsWebSocket] Global callbacks initialized");

            _isInitialized = true;
        }

        public static void Register(int handlerId, JsWebSocket socket)
        {
            Instances[handlerId] = socket;
        }

        public static void Unregister(int handlerId)
        {
            Instances.Remove(handlerId);
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnOpen(int handlerId)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                Debug.Log($"[Network] [JsWebSocket] OnOpen: id={handlerId}");
                instance.OnBrowserOpen();
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

                instance.OnBrowserMessage(data);
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
                instance.OnBrowserClose(code);
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnClose: instance not found for id={handlerId}");
            }
        }
    }
}
