using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Internal
{
    // The sockets between a guest of a P2P match and its host through the server, for a guest without a direct
    // connection: the frames of the session of the host go inside SharedSessionPeer.Tunnel over the connection of both
    // to the server session of the match (the control connection), which relays them by the index of the target.
    public interface IPeerTunnels
    {
        // A frame of a user without an open tunnel: the first frame of a guest. The host opens the tunnel inside the
        // call (and accepts its socket on its local server), and the frame goes to it.
        IViewableDelegate<int> Incoming { get; }

        // The socket to the user of remoteIndex, until the lifetime ends, the control connection is lost or the user
        // leaves the server session.
        IWebSocket Open(IReadOnlyLifetime lifetime, int remoteIndex);
    }

    public class PeerTunnels : IPeerTunnels
    {
        public PeerTunnels(INetworkConnection control)
        {
            _control = control;
        }

        private readonly INetworkConnection _control;
        private readonly Dictionary<int, TunnelPeerSocket> _sockets = new();
        private readonly ViewableDelegate<int> _incoming = new();

        public IViewableDelegate<int> Incoming => _incoming;

        public IWebSocket Open(IReadOnlyLifetime lifetime, int remoteIndex)
        {
            if (_sockets.ContainsKey(remoteIndex) == true)
                throw new InvalidOperationException($"[Network] [Tunnel] A tunnel to {remoteIndex} is already open");

            var socket = new TunnelPeerSocket(_control, remoteIndex, lifetime);
            _sockets.Add(remoteIndex, socket);
            socket.Lifetime.Listen(() => _sockets.Remove(remoteIndex));

            return socket;
        }

        public void OnFrame(SharedSessionPeer.Tunnel context)
        {
            if (_sockets.ContainsKey(context.SenderIndex) == false)
                _incoming.Invoke(context.SenderIndex);

            // Nobody wanted the tunnel (not the host, or the match is over here): the frame is dropped, the guest is
            // lost by the end of its session.
            if (_sockets.TryGetValue(context.SenderIndex, out var socket) == false)
            {
                Debug.LogWarning($"[Network] [Tunnel] A frame of {context.SenderIndex} without a tunnel: dropped");
                return;
            }

            socket.Receive(context.Frame);
        }

        // The user has left the server session or lost its connection to it (RemoteDisconnect, RemoteAway): its tunnel
        // is lost.
        public void OnPeerGone(int remoteIndex)
        {
            if (_sockets.TryGetValue(remoteIndex, out var socket) == true)
                socket.Lose($"user {remoteIndex} has left the server session");
        }

        private class TunnelPeerSocket : IWebSocket
        {
            public TunnelPeerSocket(INetworkConnection control, int remoteIndex, IReadOnlyLifetime lifetime)
            {
                _control = control;
                _remoteIndex = remoteIndex;
                _lifetime = lifetime.Child();

                // The tunnel lives inside the control connection.
                _control.Closed.Advise(_lifetime, info => Lose($"control connection lost: {info}"));
                _lifetime.Listen(() => {
                    _received.Dispose();
                    _closed.Dispose();
                });
            }

            private readonly INetworkConnection _control;
            private readonly int _remoteIndex;
            private readonly ILifetime _lifetime;
            private readonly ViewableDelegate<byte[]> _received = new();
            private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

            public IReadOnlyLifetime Lifetime => _lifetime;
            public IViewableDelegate<byte[]> Received => _received;
            public IViewableDelegate<SocketCloseInfo> Closed => _closed;

            // Open as soon as created, inside the open control connection: the host answers the first frame of a guest
            // at once. The connect of the guest completes on the next frame, as a network socket never opens
            // synchronously.
            public async UniTask Connect()
            {
                await UniTask.Yield(PlayerLoopTiming.Update, _lifetime.Token);
                Debug.Log($"[Network] [Tunnel] Connected to {_remoteIndex}");
            }

            public UniTask Send(byte[] bytes)
            {
                if (_lifetime.IsTerminated == true)
                    return UniTask.CompletedTask;

                _control.OneWay(new SharedSessionPeer.Tunnel
                {
                    TargetIndex = _remoteIndex,
                    Frame = bytes
                });

                return UniTask.CompletedTask;
            }

            public void Drop(string reason)
            {
                Lose($"dropped: {reason}");
            }

            public void Receive(byte[] frame)
            {
                if (_lifetime.IsTerminated == false)
                    _received.Invoke(frame);
            }

            public void Lose(string reason)
            {
                if (_lifetime.IsTerminated == true)
                    return;

                var info = new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"tunnel lost: {reason}");
                Debug.LogWarning($"[Network] [Tunnel] Connection to {_remoteIndex} lost: {info}");

                _closed.Invoke(info);
                _lifetime.Terminate();
            }
        }
    }
}
