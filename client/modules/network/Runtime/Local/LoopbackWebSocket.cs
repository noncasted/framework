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
        }

        private readonly string _url;
        private readonly ILocalServer _server;
        private readonly ILifetime _lifetime;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();
        private readonly Queue<byte[]> _toServer = new();
        private readonly Queue<byte[]> _toClient = new();

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
            Pump().Forget();
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

        private async UniTaskVoid Pump()
        {
            while (true)
            {
                var isCanceled = await UniTask
                                       .Yield(PlayerLoopTiming.Update, _lifetime.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                // A frame a handler adds while the queue is drained goes in the same frame, as a batch of a socket.
                while (_toServer.Count > 0 && _lifetime.IsTerminated == false)
                    Deliver(_toServer.Dequeue(), _connection.OnReceived);

                while (_toClient.Count > 0 && _lifetime.IsTerminated == false)
                    Deliver(_toClient.Dequeue(), _received.Invoke);

                if (_serverClose.HasValue == true)
                {
                    NotifyClosed(_serverClose.Value);
                    return;
                }
            }
        }

        // A failed handler is reported and the next frames go on, as the receive loop of a network socket does not
        // stop on an error of the code above it.
        private static void Deliver(byte[] bytes, Action<byte[]> handler)
        {
            try
            {
                handler(bytes);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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
