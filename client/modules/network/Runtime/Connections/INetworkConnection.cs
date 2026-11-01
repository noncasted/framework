using Cysharp.Threading.Tasks;

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
}
