#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // IWebRtcPeer on libdatachannel (Windows, Linux: the desktop players and editors). The library calls back from its
    // own threads: the events go to IWebRtcPeerEvents from there, WebRtcPeerSocket queues them for the main thread.
    [AutoStaticsCleanup]
    public partial class NativeWebRtcPeer : IWebRtcPeer
    {
        public NativeWebRtcPeer(WebRtcPeerOptions options, IWebRtcPeerEvents events)
        {
            Initialize();

            _events = events;
            _key = Interlocked.Increment(ref _lastKey);
            Peers[_key] = this;

            _connection = CreateConnection(options);
            NativeWebRtcApi.SetUserPointer(_connection, (IntPtr)_key);

            Check(NativeWebRtcApi.SetLocalDescriptionCallback(_connection, OnDescriptionCallback), "description");
            Check(NativeWebRtcApi.SetLocalCandidateCallback(_connection, OnCandidateCallback), "candidate");
            Check(NativeWebRtcApi.SetStateChangeCallback(_connection, OnStateCallback), "state");
            Check(NativeWebRtcApi.SetDataChannelCallback(_connection, OnDataChannelCallback), "data channel");
        }

        // The live peers by the user pointer the library hands back to the callbacks.
        private static readonly ConcurrentDictionary<int, NativeWebRtcPeer> Peers = new();

        // The log of the library comes from its threads: written on the main thread, as the log handlers of the editor
        // (the clones of Multiplayer Play Mode) read the editor state.
        private static readonly ConcurrentQueue<(NativeWebRtcApi.LogLevel Level, string Text)> Logs = new();

        // Kept in static fields: the library holds the function pointers for as long as the peers live. Valid across
        // a code reload, nothing to reset.
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.LogCallback OnLogCallback = OnLog;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.DescriptionCallback OnDescriptionCallback = OnDescription;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.CandidateCallback OnCandidateCallback = OnCandidate;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.StateCallback OnStateCallback = OnState;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.DataChannelCallback OnDataChannelCallback = OnDataChannel;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.ChannelCallback OnOpenCallback = OnOpen;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.ChannelCallback OnClosedCallback = OnClosed;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.ErrorCallback OnErrorCallback = OnError;
        [NoAutoStaticsCleanup] private static readonly NativeWebRtcApi.MessageCallback OnMessageCallback = OnMessage;

        private static int _lastKey;
        private static bool _isInitialized;

        private readonly IWebRtcPeerEvents _events;
        private readonly int _key;
        private readonly int _connection;

        // Set on the main thread (the offering side) or on a thread of the library (the answering side, the channel of
        // the peer); read on both.
        private int _channel = -1;

        // Once each: the open of a channel may be seen both by its callback and by the check after the callbacks are set.
        private int _isOpened;
        private int _isLost;
        private bool _isDisposed;

        public int MaxMessageSize => Check(NativeWebRtcApi.MaxMessageSize(Channel()), "max message size");

        public void CreateChannel(string label)
        {
            if (_channel >= 0)
                throw new InvalidOperationException($"[Network] [NativeWebRtc] The channel is already created: {label}");

            var channel = Check(NativeWebRtcApi.CreateDataChannel(_connection, NativeWebRtcApi.Utf8(label)),
                "create data channel");

            SetupChannel(channel);
        }

        public void SetRemoteDescription(string type, string sdp)
        {
            Check(NativeWebRtcApi.SetRemoteDescription(_connection, NativeWebRtcApi.Utf8(sdp), NativeWebRtcApi.Utf8(type)),
                "set remote description");
        }

        public void AddRemoteCandidate(string candidate, string mid)
        {
            Check(NativeWebRtcApi.AddRemoteCandidate(_connection, NativeWebRtcApi.Utf8(candidate), NativeWebRtcApi.Utf8(mid)),
                "add remote candidate");
        }

        public void Send(byte[] message)
        {
            Check(NativeWebRtcApi.SendMessage(Channel(), message, message.Length), "send");
        }

        public void Dispose()
        {
            if (_isDisposed == true)
                return;

            _isDisposed = true;

            // Out of the table first: a callback that is still running finds no peer and drops its event.
            Peers.TryRemove(_key, out _);

            if (_channel >= 0)
                NativeWebRtcApi.Delete(_channel);

            NativeWebRtcApi.DeletePeerConnection(_connection);
        }

        private int Channel()
        {
            if (_channel < 0)
                throw new InvalidOperationException("[Network] [NativeWebRtc] The channel is not created");

            return _channel;
        }

        private void SetupChannel(int channel)
        {
            if (Interlocked.CompareExchange(ref _channel, channel, -1) != -1)
                throw new InvalidOperationException($"[Network] [NativeWebRtc] A second data channel: {channel}");

            NativeWebRtcApi.SetUserPointer(channel, (IntPtr)_key);

            Check(NativeWebRtcApi.SetOpenCallback(channel, OnOpenCallback), "open");
            Check(NativeWebRtcApi.SetClosedCallback(channel, OnClosedCallback), "closed");
            Check(NativeWebRtcApi.SetErrorCallback(channel, OnErrorCallback), "error");
            Check(NativeWebRtcApi.SetMessageCallback(channel, OnMessageCallback), "message");

            // The channel of the peer may be open before its callback is set.
            if (NativeWebRtcApi.IsOpen(channel) == true)
                Opened();
        }

        private void Opened()
        {
            if (Interlocked.Exchange(ref _isOpened, 1) == 0)
                _events.OnOpen();
        }

        private void Lost(string reason)
        {
            if (Interlocked.Exchange(ref _isLost, 1) == 0)
                _events.OnLost(reason);
        }

        private int CreateConnection(WebRtcPeerOptions options)
        {
            var servers = new IntPtr[options.IceServers.Count];
            var serversHandle = default(GCHandle);

            try
            {
                for (var i = 0; i < servers.Length; i++)
                    servers[i] = Marshal.StringToCoTaskMemUTF8(options.IceServers[i]);

                serversHandle = GCHandle.Alloc(servers, GCHandleType.Pinned);

                var configuration = new NativeWebRtcApi.Configuration(
                    servers.Length > 0 ? serversHandle.AddrOfPinnedObject() : IntPtr.Zero,
                    servers.Length);

                // The library copies the configuration.
                return Check(NativeWebRtcApi.CreatePeerConnection(ref configuration), "create peer connection");
            }
            finally
            {
                if (serversHandle.IsAllocated == true)
                    serversHandle.Free();

                foreach (var server in servers)
                    Marshal.FreeCoTaskMem(server);
            }
        }

        private static int Check(int result, string call)
        {
            return NativeWebRtcApi.Check(result, call);
        }

        private static void Initialize()
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

            foreach (var peer in Peers.Values)
                peer.Dispose();

            NativeWebRtcApi.InitLogger(NativeWebRtcApi.LogLevel.None, null);
            _isInitialized = false;
        }
#endif

        // The callbacks run on the threads of the library: an exception must not cross into native code.
        private static void Dispatch(IntPtr user, Action<NativeWebRtcPeer> action)
        {
            try
            {
                // A peer disposed while the library was calling back: its events are no longer wanted.
                if (Peers.TryGetValue((int)user, out var peer) == false)
                    return;

                action(peer);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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
                    if (line.Level <= NativeWebRtcApi.LogLevel.Error)
                        Debug.LogError(line.Text);
                    else if (line.Level == NativeWebRtcApi.LogLevel.Warning)
                        Debug.LogWarning(line.Text);
                    else
                        Debug.Log(line.Text);
                }
            }
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.DescriptionCallback))]
        private static void OnDescription(int pc, IntPtr sdp, IntPtr type, IntPtr user)
        {
            var sdpText = NativeWebRtcApi.String(sdp);
            var typeText = NativeWebRtcApi.String(type);
            Dispatch(user, peer => peer._events.OnLocalDescription(typeText, sdpText));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.CandidateCallback))]
        private static void OnCandidate(int pc, IntPtr candidate, IntPtr mid, IntPtr user)
        {
            var candidateText = NativeWebRtcApi.String(candidate);
            var midText = NativeWebRtcApi.String(mid);
            Dispatch(user, peer => peer._events.OnLocalCandidate(candidateText, midText));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.StateCallback))]
        private static void OnState(int pc, NativeWebRtcApi.State state, IntPtr user)
        {
            if (state != NativeWebRtcApi.State.Disconnected &&
                state != NativeWebRtcApi.State.Failed &&
                state != NativeWebRtcApi.State.Closed)
                return;

            Dispatch(user, peer => peer.Lost($"connection {state}"));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.DataChannelCallback))]
        private static void OnDataChannel(int pc, int dc, IntPtr user)
        {
            Dispatch(user, peer => peer.SetupChannel(dc));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.ChannelCallback))]
        private static void OnOpen(int id, IntPtr user)
        {
            Dispatch(user, peer => peer.Opened());
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.ChannelCallback))]
        private static void OnClosed(int id, IntPtr user)
        {
            Dispatch(user, peer => peer.Lost("channel closed"));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.ErrorCallback))]
        private static void OnError(int id, IntPtr error, IntPtr user)
        {
            var errorText = NativeWebRtcApi.String(error);
            Dispatch(user, peer => peer.Lost($"channel error: {errorText}"));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.MessageCallback))]
        private static void OnMessage(int id, IntPtr message, int size, IntPtr user)
        {
            // A negative size is a text message: the channel carries frames of the protocol only.
            if (size < 0)
            {
                Dispatch(user, peer => peer.Lost("text message on the binary channel"));
                return;
            }

            var bytes = new byte[size];
            Marshal.Copy(message, bytes, 0, size);
            Dispatch(user, peer => peer._events.OnMessage(bytes));
        }
    }
}
#endif
