#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using AOT;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // The global state of libdatachannel: initialized by the first peer, its log written on the main thread.
    [AutoStaticsCleanup]
    internal static partial class NativeWebRtcLibrary
    {
        // The log of the library comes from its threads: written on the main thread, as the log handlers of the editor
        // (the clones of Multiplayer Play Mode) read the editor state.
        private static readonly ConcurrentQueue<(NativeWebRtcApi.LogLevel Level, string Text)> Logs = new();

        // Kept in a static field: the library holds the function pointer. Valid across a code reload, nothing to reset.
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.LogCallback OnLogCallback = OnLog;

        private static bool _isInitialized;

        public static void Initialize()
        {
            if (_isInitialized == true)
                return;

            _isInitialized = true;

            // Errors only: the warnings of the ICE agent (a candidate pair that cannot send) are a normal part of the
            // negotiation.
            NativeWebRtcApi.InitLogger(NativeWebRtcApi.LogLevel.Error, OnLogCallback);
            WriteLogs().NoAwait();

            // The threads of the library must not call into a domain that is unloaded, and a connection alive at exit is
            // destroyed by the static destructors of the library, which can crash on the way out (code 139 of the editor).
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.quitting += Shutdown;
#else
            Application.quitting += Shutdown;
#endif
        }

        private static void Shutdown()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.EditorApplication.quitting -= Shutdown;
#else
            Application.quitting -= Shutdown;
#endif

            NativeWebRtcCallbacks.DisposeAll();

            NativeWebRtcApi.InitLogger(NativeWebRtcApi.LogLevel.None, null);
            _isInitialized = false;
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.LogCallback))]
        private static void OnLog(NativeWebRtcApi.LogLevel level, IntPtr message)
        {
            Logs.Enqueue((level, $"[Network] [libdatachannel] {NativeWebRtcApi.String(message)}"));
        }

        // While the library is initialized: the lines of a frame are written at its start.
        private static async UniTaskVoid WriteLogs()
        {
            while (_isInitialized == true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                while (Logs.TryDequeue(out var line) == true)
                {
                    // A failure of a connection reaches the code through the state of its peer, and the library also
                    // logs its errors for what is normal: a transport that closes while the other side has already closed
                    // (SCTP write on a DTLS after its shutdown). Only a fatal one is an error of the application.
                    if (line.Level <= NativeWebRtcApi.LogLevel.Fatal)
                        Debug.LogError(line.Text);
                    else if (line.Level <= NativeWebRtcApi.LogLevel.Warning)
                        Debug.LogWarning(line.Text);
                    else
                        Debug.Log(line.Text);
                }
            }
        }
    }
}
#endif
