#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // IWebRtcPeer of the browser (WebGL): RTCPeerConnection through JsWebRtcPeer.jslib. The browser calls back from its
    // own events, on the main thread but outside the player loop; WebRtcPeerSocket queues them for the next frame.
    [AutoStaticsCleanup]
    public partial class JsWebRtcPeer : IWebRtcPeer
    {
        public JsWebRtcPeer(WebRtcPeerOptions options, IWebRtcPeerEvents events)
        {
            if (_isInitialized == false)
            {
                JsWebRtcSetCallbacks(OnDescription, OnCandidate, OnOpen, OnMessage, OnLost);
                _isInitialized = true;
            }

            _events = events;
            _id = JsWebRtcCreate(string.Join("\n", options.IceServers));
            Peers[_id] = this;
        }

        [NoAutoStaticsCleanup] private static readonly ILog _log = Logs.Of<JsWebRtcPeer>();
        private static readonly Dictionary<int, JsWebRtcPeer> Peers = new();
        private static bool _isInitialized;

        // JsWebRtcSend: the channel is not open.
        private const int SendNotOpen = -1;

        private readonly IWebRtcPeerEvents _events;
        private readonly int _id;

        private bool _isDisposed;

        public int MaxMessageSize => JsWebRtcMaxMessageSize(_id);

        public void CreateChannel(string label)
        {
            JsWebRtcCreateChannel(_id, label);
        }

        public void SetRemoteDescription(string type, string sdp)
        {
            JsWebRtcSetRemoteDescription(_id, type, sdp);
        }

        public void AddRemoteCandidate(string candidate, string mid)
        {
            JsWebRtcAddRemoteCandidate(_id, candidate, mid);
        }

        public void Send(byte[] message)
        {
            var handle = GCHandle.Alloc(message, GCHandleType.Pinned);

            try
            {
                var result = JsWebRtcSend(_id, handle.AddrOfPinnedObject(), message.Length);

                if (result == SendNotOpen)
                    throw new InvalidOperationException($"[Network] [JsWebRtc] Send on a channel that is not open: id={_id}");
            }
            finally
            {
                handle.Free();
            }
        }

        public void Dispose()
        {
            if (_isDisposed == true)
                return;

            _isDisposed = true;
            Peers.Remove(_id);
            JsWebRtcFree(_id);
        }

        private static void Dispatch(int id, Action<IWebRtcPeerEvents> action)
        {
            // An exception must not reach the browser event loop.
            try
            {
                action(Peers[id]._events);
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, IntPtr>))]
        private static void OnDescription(int id, IntPtr type, IntPtr sdp)
        {
            var typeText = Marshal.PtrToStringUTF8(type);
            var sdpText = Marshal.PtrToStringUTF8(sdp);
            Dispatch(id, events => events.OnLocalDescription(typeText, sdpText));
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, IntPtr>))]
        private static void OnCandidate(int id, IntPtr candidate, IntPtr mid)
        {
            var candidateText = Marshal.PtrToStringUTF8(candidate);
            var midText = Marshal.PtrToStringUTF8(mid);
            Dispatch(id, events => events.OnLocalCandidate(candidateText, midText));
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnOpen(int id)
        {
            Dispatch(id, events => events.OnOpen());
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        private static void OnMessage(int id, IntPtr buffer, int length)
        {
            var bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, length);
            Dispatch(id, events => events.OnMessage(bytes));
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr>))]
        private static void OnLost(int id, IntPtr reason)
        {
            var reasonText = Marshal.PtrToStringUTF8(reason);
            Dispatch(id, events => events.OnLost(reasonText));
        }

        [DllImport("__Internal")]
        private static extern void JsWebRtcSetCallbacks(
            Action<int, IntPtr, IntPtr> onDescription,
            Action<int, IntPtr, IntPtr> onCandidate,
            Action<int> onOpen,
            Action<int, IntPtr, int> onMessage,
            Action<int, IntPtr> onLost);

        [DllImport("__Internal")]
        private static extern int JsWebRtcCreate(string iceServers);

        [DllImport("__Internal")]
        private static extern void JsWebRtcCreateChannel(int id, string label);

        [DllImport("__Internal")]
        private static extern void JsWebRtcSetRemoteDescription(int id, string type, string sdp);

        [DllImport("__Internal")]
        private static extern void JsWebRtcAddRemoteCandidate(int id, string candidate, string mid);

        [DllImport("__Internal")]
        private static extern int JsWebRtcSend(int id, IntPtr buffer, int length);

        [DllImport("__Internal")]
        private static extern int JsWebRtcMaxMessageSize(int id);

        [DllImport("__Internal")]
        private static extern void JsWebRtcFree(int id);
    }
}
#endif
