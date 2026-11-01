using System;
using System.Runtime.InteropServices;

namespace Internal
{
    // The functions of JsWebSocket.jslib.
    internal static class JsWebSocketInterop
    {
        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        public static extern void JsWebSocketSetOnOpen(Action<int> callback);

        [DllImport("__Internal")]
        public static extern void JsWebSocketSetOnMessage(Action<int, IntPtr, int> callback);

        [DllImport("__Internal")]
        public static extern void JsWebSocketSetOnError(Action<int, IntPtr> callback);

        [DllImport("__Internal")]
        public static extern void JsWebSocketSetOnClose(Action<int, int> callback);

        [DllImport("__Internal")]
        public static extern int JsWebSocketCreate(string url);

        [DllImport("__Internal")]
        public static extern int JsWebSocketConnect(int id);

        [DllImport("__Internal")]
        public static extern int JsWebSocketSend(int id, IntPtr buffer, int length);

        [DllImport("__Internal")]
        public static extern int JsWebSocketClose(int id, int code, string reason);

        [DllImport("__Internal")]
        public static extern int JsWebSocketFree(int id);

#else
        public static void JsWebSocketSetOnOpen(Action<int> callback) =>
            throw Internal.JsLibStub.Missing();

        public static void JsWebSocketSetOnMessage(Action<int, IntPtr, int> callback) =>
            throw Internal.JsLibStub.Missing();

        public static void JsWebSocketSetOnError(Action<int, IntPtr> callback) =>
            throw Internal.JsLibStub.Missing();

        public static void JsWebSocketSetOnClose(Action<int, int> callback) =>
            throw Internal.JsLibStub.Missing();

        public static int JsWebSocketCreate(string url) =>
            throw Internal.JsLibStub.Missing();

        public static int JsWebSocketConnect(int id) =>
            throw Internal.JsLibStub.Missing();

        public static int JsWebSocketSend(int id, IntPtr buffer, int length) =>
            throw Internal.JsLibStub.Missing();

        public static int JsWebSocketClose(int id, int code, string reason) =>
            throw Internal.JsLibStub.Missing();

        public static int JsWebSocketFree(int id) =>
            throw Internal.JsLibStub.Missing();

#endif
    }
}
