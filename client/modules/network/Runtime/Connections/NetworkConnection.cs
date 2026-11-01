using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public interface INetworkConnection
    {
        IConnectionReader Reader { get; }
        IConnectionWriter Writer { get; }

        /// <summary>
        /// The connection was lost not on our initiative. There is no reconnect: pending
        /// requests have already failed with OperationCanceledException, the calling code decides what to do next.
        /// </summary>
        IViewableDelegate<SocketCloseInfo> Closed { get; }

        NetworkTraffic Traffic { get; }

        // Opens the socket. ConnectionFailedException when it does not open (the connection is closed then); a loss
        // at any moment after it opens - even before Run returns - closes the connection and raises Closed.
        UniTask Run(IReadOnlyLifetime lifetime, string url);

        // The connection is found lost by this client (IWebSocket.Drop): Closed follows as for any loss.
        void Drop(string reason);
    }

    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(
            INetworkCommandsCollection commands,
            PlatformOptions platformOptions,
            IClientOutdatedNotice outdatedNotice,
            ILocalServers localServers,
            IPeerSockets peerSockets,
            NetworkProtocolHash protocolHash)
        {
            _platformOptions = platformOptions;
            _protocolHash = protocolHash.Value;
            _localServers = localServers;
            _peerSockets = peerSockets;
            _outdatedNotice = outdatedNotice;
            _traffic = new NetworkTraffic();
            _reader = new ConnectionReader(_traffic);
            _writer = new ConnectionWriter(_traffic);
            _dispatcher = new NetworkCommandsDispatcher(commands, this);
        }

        private IWebSocket _webSocket;

        private readonly NetworkCommandsDispatcher _dispatcher;
        private readonly ConnectionReader _reader;
        private readonly ConnectionWriter _writer;
        private readonly NetworkTraffic _traffic;
        private readonly PlatformOptions _platformOptions;
        private readonly IClientOutdatedNotice _outdatedNotice;
        private readonly ILocalServers _localServers;
        private readonly IPeerSockets _peerSockets;
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

        // The protocol this client was built with, sent in the query of the upgrade. A field: a play test replaces it
        // to check the outdated client.
        private readonly string _protocolHash;

        // A connect that hangs (a proxy that accepts TCP but has no server behind it, a stalled upgrade) is a failed
        // one: without a bound the caller waited forever. A field: a test shortens it.
        private readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(15);

        public IConnectionReader Reader => _reader;
        public IConnectionWriter Writer => _writer;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;
        public NetworkTraffic Traffic => _traffic;

        public async UniTask Run(IReadOnlyLifetime lifetime, string url)
        {
            // Ended when the connect fails: the timeout below only stops the wait, the socket went on connecting
            // (and could open) for a connection that has failed.
            var socketLifetime = lifetime.Child();

            _webSocket = CreateWebSocket(AppendProtocol(url));

            // A server of another protocol accepts the socket and closes it at once, maybe before Connect returns:
            // subscribed first, whatever else listens to the loss.
            _webSocket.Closed.Advise(lifetime, OnSocketClosed);

            // Reading is subscribed before connecting: authorization travels in the upgrade, and the server sends projections
            // right away. The first frames can arrive together with the upgrade response and hit Received
            // while still inside Connect (or between onopen and the next frame in WebGL) - without a subscriber
            // they were lost and the profile stayed null.
            _dispatcher.Run(lifetime);
            _reader.Run(lifetime, _webSocket);

            // The writer and the listeners of the loss are subscribed before connecting too: the socket may be lost
            // inside Connect (DefaultWebSocket: a socket dropped between the upgrade and the next frame fails its first
            // receive synchronously, and the loss is reported before Connect returns). Subscribed after it, they
            // missed that loss: the connection stayed open forever, IsClosed false, the authorization request waited
            // for its timeout. The writer goes first: by the time the loss reaches game code, pending requests are
            // already completed.
            _writer.Run(lifetime, _webSocket);
            _webSocket.Closed.Advise(lifetime, info => _closed.Invoke(info));

            try
            {
                // Connecting runs in parallel with menu loading: the span does not go on the stack, otherwise unrelated
                // stages would nest into the socket wait.
                using (GameProfiler.Concurrent("Socket connect"))
                    await _webSocket.Connect().Timeout(_connectTimeout, DelayType.Realtime);
            }
            // The socket never opened (refused, dropped during the upgrade): nothing is answered on this connection.
            // The end of the lifetime is not a failure: its cancellation goes up as it is.
            catch (Exception exception) when (lifetime.IsTerminated == false)
            {
                socketLifetime.Terminate();
                _writer.Close();
                throw new ConnectionFailedException(url, exception);
            }

            return;

            IWebSocket CreateWebSocket(string socketUrl)
            {
                // The offline mode: a server inside this process (LocalServerUrl), on every platform.
                if (LocalServerUrl.IsLocal(socketUrl) == true)
                    return new LoopbackWebSocket(socketUrl, _localServers.Get(socketUrl), socketLifetime);

                // A P2P match: the data channel to its host, on every platform.
                if (PeerUrl.IsPeer(socketUrl) == true)
                    return _peerSockets.Create(socketUrl, socketLifetime);

                // ClientWebSocket does not work in WebGL, and referencing it pulls System.Net, TLS and Mono.Security into the build.
#if UNITY_EDITOR || !UNITY_WEBGL
                if (_platformOptions.IsEditor == true)
                    return new DefaultWebSocket(socketUrl, socketLifetime);
#endif

                switch (_platformOptions.PlatformType)
                {
                    case PlatformType.Website:
                    case PlatformType.ItchIO:
                    case PlatformType.Telegram:
                        return new JsWebSocket(socketUrl, socketLifetime);
#if UNITY_EDITOR || !UNITY_WEBGL
                    case PlatformType.IOS:
                    case PlatformType.Android:
                    case PlatformType.Steam:
                        return new DefaultWebSocket(socketUrl, socketLifetime);
#endif
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Drop(string reason)
        {
            if (_webSocket == null)
                throw new InvalidOperationException($"[Network] The connection is dropped before it runs: {reason}");

            _webSocket.Drop(reason);
        }

        private string AppendProtocol(string url)
        {
            var separator = url.Contains('?') == true ? '&' : '?';
            return $"{url}{separator}{ProtocolVersion.QueryKey}={_protocolHash}";
        }

        private void OnSocketClosed(SocketCloseInfo info)
        {
            if (info.Code == ProtocolVersion.ClientOutdatedCloseCode)
                _outdatedNotice.Show();
        }
    }

    // NetworkConnection.Run: the socket did not open (connect refused, the network dropped it during the upgrade).
    // The connection is closed (IConnectionWriter.IsClosed); INetworkConnection.Closed is not raised - it is for a
    // connection that was established. A caller that retries (a return into a match) treats it as a lost connection.
    public class ConnectionFailedException : Exception
    {
        public ConnectionFailedException(string url, Exception inner)
            : base($"[Network] The socket to {url} did not open: {inner.GetType().Name} {inner.Message}", inner)
        {
        }
    }

    public static class NetworkConnectionExtensions
    {
        public static IScopeBuilder AddNetworkConnection(this IScopeBuilder builder)
        {
            builder.Register<NetworkConnection>()
                   .As<INetworkConnection>()
                   .AsSelf();

            builder.Register<NetworkCommandsCollection>()
                   .AsSelfResolvable()
                   .As<INetworkCommandsCollection>();

            return builder;
        }

        public static void OneWay(this INetworkConnection connection, INetworkContext value)
        {
            connection.Writer.WriteOneWay(value);
        }

        // A request of the session layer; failures as in IConnectionWriter.WriteRequest. A closed connection is a
        // cancelled operation: whatever was being done over the session (entering it, creating an entity) stops
        // there, and the loss itself is handled by the listener of Closed.
        public static UniTask<T> Request<T>(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Writer.WriteRequest<T>(commandContext);
        }

        public static UniTask<EmptyResponse> Request(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Request<EmptyResponse>(commandContext);
        }

        public static UniTask ForceSendAll(this INetworkConnection connection)
        {
            return connection.Writer.ForceSendAll();
        }
    }
}