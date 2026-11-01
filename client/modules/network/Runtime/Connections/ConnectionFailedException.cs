using System;

namespace Internal
{
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
}
