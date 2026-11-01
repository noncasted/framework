using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(
            INetworkCommandsCollection commands,
            IClientOutdatedNotice outdatedNotice,
            IWebSocketFactory socketFactory,
            NetworkProtocolHash protocolHash)
        {
            _protocolHash = protocolHash.Value;
            _socketFactory = socketFactory;
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
        private readonly IClientOutdatedNotice _outdatedNotice;
        private readonly IWebSocketFactory _socketFactory;
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

            _webSocket = _socketFactory.Create(AppendProtocol(url), socketLifetime);

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
}
