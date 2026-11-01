using System;

namespace Internal
{
    public class WebSocketFactory : IWebSocketFactory
    {
        public WebSocketFactory(PlatformOptions platformOptions, ILocalServers localServers, IPeerSockets peerSockets)
        {
            _platformOptions = platformOptions;
            _localServers = localServers;
            _peerSockets = peerSockets;
        }

        private readonly PlatformOptions _platformOptions;
        private readonly ILocalServers _localServers;
        private readonly IPeerSockets _peerSockets;

        public IWebSocket Create(string url, IReadOnlyLifetime lifetime)
        {
            // The offline mode: a server inside this process (LocalServerUrl), on every platform.
            if (LocalServerUrl.IsLocal(url) == true)
                return new LoopbackWebSocket(url, _localServers.Get(url), lifetime);

            // A P2P match: the data channel to its host, on every platform.
            if (PeerUrl.IsPeer(url) == true)
                return _peerSockets.Create(url, lifetime);

            // ClientWebSocket does not work in WebGL, and referencing it pulls System.Net, TLS and Mono.Security into the build.
#if UNITY_EDITOR || !UNITY_WEBGL
            if (_platformOptions.IsEditor == true)
                return new DefaultWebSocket(url, lifetime);
#endif

            switch (_platformOptions.PlatformType)
            {
                case PlatformType.Website:
                case PlatformType.ItchIO:
                case PlatformType.Telegram:
                    return new JsWebSocket(url, lifetime);
#if UNITY_EDITOR || !UNITY_WEBGL
                case PlatformType.IOS:
                case PlatformType.Android:
                case PlatformType.Steam:
                    return new DefaultWebSocket(url, lifetime);
#endif
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
