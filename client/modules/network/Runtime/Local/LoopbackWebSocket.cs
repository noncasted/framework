using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // A socket to a local server (ILocalServer) in this process. Frames go both ways through queues drained once a
    // frame on the main thread: nothing is delivered inside Send, so the client stack sees the same order and the same
    // asynchrony as with a network socket.
    public class LoopbackWebSocket : IWebSocket, ILocalClient
    {
        public LoopbackWebSocket(string url, ILocalServer server, IReadOnlyLifetime lifetime)
        {
            _url = url;
            _server = server;
            _lifetime = lifetime.Child();
            _toServer = new SocketInbox<byte[]>(_lifetime, bytes => _connection.OnReceived(bytes));
            _toClient = new SocketInbox<byte[]>(_lifetime, bytes => _received.Invoke(bytes));
        }

        private readonly string _url;
        private readonly ILocalServer _server;
        private readonly ILifetime _lifetime;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();
        private readonly SocketInbox<byte[]> _toServer;
        private readonly SocketInbox<byte[]> _toClient;

        private ILocalServerConnection _connection;
        private bool _isClosedNotified;
        private SocketCloseInfo? _serverClose;

        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public async UniTask Connect()
        {
            // Opened on the next frame, as a network socket never opens synchronously.
            await UniTask.Yield(PlayerLoopTiming.Update, _lifetime.Token);

            Debug.Log($"[Network] [Loopback] Connected: {_url}");

            _connection = _server.Accept(_lifetime, this);
            SocketPump.Run(_lifetime, Pump).NoAwait();
        }

        public UniTask Send(byte[] bytes)
        {
            if (_lifetime.IsTerminated == false)
                _toServer.Enqueue(bytes);

            return UniTask.CompletedTask;
        }

        public void Drop(string reason)
        {
            NotifyClosed(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"dropped: {reason}"));
        }

        void ILocalClient.Send(byte[] bytes)
        {
            if (_lifetime.IsTerminated == false)
                _toClient.Enqueue(bytes);
        }

        // The frames the server sent before it closed the socket reach the client first, as the close frame of a
        // network socket comes after them.
        void ILocalClient.Close(SocketCloseInfo info)
        {
            _serverClose ??= info;
        }

        private void Pump()
        {
            _toServer.Drain();
            _toClient.Drain();

            if (_serverClose.HasValue == true)
                NotifyClosed(_serverClose.Value);
        }

        private void NotifyClosed(SocketCloseInfo info)
        {
            if (_isClosedNotified == true || _lifetime.IsTerminated == true)
                return;

            _isClosedNotified = true;

            Debug.Log($"[Network] [Loopback] Closed: {_url} {info}");

            _closed.Invoke(info);
            _lifetime.Terminate();
        }
    }
}
