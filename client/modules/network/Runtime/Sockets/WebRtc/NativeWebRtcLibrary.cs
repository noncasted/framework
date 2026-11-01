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

#if UNITY_EDITOR
            // The threads of the library must not call into a domain that is unloaded: every peer goes before a reload.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

#if UNITY_EDITOR
        private static void Shutdown()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;

            NativeWebRtcCallbacks.DisposeAll();

            NativeWebRtcApi.InitLogger(NativeWebRtcApi.LogLevel.None, null);
            _isInitialized = false;
        }
#endif

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
                    if (line.Level <= NativeWebRtcApi.LogLevel.Error)
                        Debug.LogError(line.Text);
                    else if (line.Level == NativeWebRtcApi.LogLevel.Warning)
                        Debug.LogWarning(line.Text);
                    else
                        Debug.Log(line.Text);
                }
            }
        }
    }
}
#endif
