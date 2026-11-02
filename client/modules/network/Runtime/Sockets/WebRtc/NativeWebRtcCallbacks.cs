#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // The callbacks of libdatachannel: one set of static functions, each finds its NativeWebRtcPeer by the user pointer.
    [AutoStaticsCleanup]
    internal static partial class NativeWebRtcCallbacks
    {
        // The live peers by the user pointer the library hands back to the callbacks.
        private static readonly ConcurrentDictionary<int, NativeWebRtcPeer> Peers = new();

        // Kept in static fields: the library holds the function pointers for as long as the peers live. Valid across
        // a code reload, nothing to reset.
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.DescriptionCallback OnDescriptionCallback = OnDescription;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.CandidateCallback OnCandidateCallback = OnCandidate;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.StateCallback OnStateCallback = OnState;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.DataChannelCallback OnDataChannelCallback = OnDataChannel;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.ChannelCallback OnOpenCallback = OnOpen;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.ChannelCallback OnClosedCallback = OnClosed;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.ErrorCallback OnErrorCallback = OnError;
        [NoAutoStaticsCleanup] public static readonly NativeWebRtcApi.MessageCallback OnMessageCallback = OnMessage;

        private static int _lastKey;

        // The user pointer of the peer.
        public static int Register(NativeWebRtcPeer peer)
        {
            var key = Interlocked.Increment(ref _lastKey);
            Peers[key] = peer;
            return key;
        }

        public static void Unregister(int key)
        {
            Peers.TryRemove(key, out _);
        }

        public static void DisposeAll()
        {
            foreach (var peer in Peers.Values)
                peer.Dispose();
        }

        // The callbacks run on the threads of the library: an exception must not cross into native code.
        private static void Dispatch(IntPtr user, Action<NativeWebRtcPeer> action, Action onNoPeer = null)
        {
            try
            {
                // A peer disposed while the library was calling back: its events are no longer wanted.
                if (Peers.TryGetValue((int)user, out var peer) == false)
                {
                    onNoPeer?.Invoke();
                    return;
                }

                action(peer);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.DescriptionCallback))]
        private static void OnDescription(int pc, IntPtr sdp, IntPtr type, IntPtr user)
        {
            var sdpText = NativeWebRtcApi.String(sdp);
            var typeText = NativeWebRtcApi.String(type);
            Dispatch(user, peer => peer.Events.OnLocalDescription(typeText, sdpText));
        }

        [MonoPInvokeCallback(typeof(NativeWebRtcApi.CandidateCallback))]
        private static void OnCandidate(int pc, IntPtr candidate, IntPtr mid, IntPtr user)
        {
            var candidateText = NativeWebRtcApi.String(candidate);
            var midText = NativeWebRtcApi.String(mid);
            Dispatch(user, peer => peer.Events.OnLocalCandidate(candidateText, midText));
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
            // The library keeps a channel it handed over until it is deleted, and a disposed peer will not do that.
            Dispatch(user,
                peer => peer.SetupChannel(dc),
                () => NativeWebRtcApi.Check(NativeWebRtcApi.Delete(dc), "delete a channel of a disposed peer"));
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
            Dispatch(user, peer => peer.Events.OnMessage(bytes));
        }
    }
}
#endif
