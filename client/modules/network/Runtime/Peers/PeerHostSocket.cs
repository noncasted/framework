using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // The socket of a guest of a P2P match to its host: a direct data channel when the negotiation opens it, the tunnel
    // through the server otherwise (or when the direct connection is not allowed). Chosen once, in Connect; the client
    // stack above sees one socket either way.
    public class PeerHostSocket : IWebSocket
    {
        public PeerHostSocket(
            ISessionControl control,
            int hostIndex,
            IPeerMatch match,
            WebRtcPeerOptions options,
            bool isDirectAllowed,
            IReadOnlyLifetime lifetime)
        {
            _control = control;
            _hostIndex = hostIndex;
            _match = match;
            _options = options;
            _isDirectAllowed = isDirectAllowed;
            _lifetime = lifetime.Child();
        }

        private readonly ISessionControl _control;
        private readonly int _hostIndex;
        private readonly IPeerMatch _match;
        private readonly WebRtcPeerOptions _options;
        private readonly bool _isDirectAllowed;
        private readonly ILifetime _lifetime;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

        private IWebSocket _socket;

        public PeerRoute Route { get; private set; }
        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public async UniTask Connect()
        {
            if (_socket != null)
                throw new InvalidOperationException("[Network] [Peers] Connect is called twice");

            if (_isDirectAllowed == true && WebRtcPeers.IsSupported(Application.platform) == true)
                _socket = await TryDirect();

            if (_socket == null)
            {
                Route = PeerRoute.Tunnel;
                _socket = Attach(_control.Tunnels.Open(_lifetime, _hostIndex));
                await _socket.Connect();
            }

            Debug.Log($"[Network] [Peers] Connected to the host {_hostIndex}: {Route}");
            _match.SetRoute(_control.LocalIndex, Route);
        }

        public UniTask Send(byte[] bytes)
        {
            return _socket.Send(bytes);
        }

        public void Drop(string reason)
        {
            _socket.Drop(reason);
        }

        // The failure of the negotiation is expected (NAT, no STUN answer): the attempt ends, the tunnel follows.
        private async UniTask<IWebSocket> TryDirect()
        {
            var attempt = _lifetime.Child();
            var direct = new WebRtcPeerSocket(WebRtcPeerRole.Offer, _control.Signaling.Open(attempt, _hostIndex), _options, attempt);

            try
            {
                await direct.Connect();
            }
            catch (WebRtcConnectFailedException exception)
            {
                Debug.LogWarning($"[Network] [Peers] No direct connection to the host {_hostIndex}: {exception.Message}");
                attempt.Terminate();
                return null;
            }
            catch
            {
                attempt.Terminate();
                throw;
            }

            Route = PeerRoute.Direct;
            return Attach(direct);
        }

        private IWebSocket Attach(IWebSocket socket)
        {
            socket.Received.Advise(_lifetime, _received.Invoke);
            socket.Closed.Advise(_lifetime, _closed.Invoke);
            return socket;
        }
    }

    public enum PeerRoute
    {
        None,
        Direct,
        Tunnel
    }
}
