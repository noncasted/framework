using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    /// <summary>
    /// Threading contract: <see cref="Received"/> and <see cref="Closed"/> are invoked on the main
    /// thread, and <see cref="Connect"/> and <see cref="Send"/> return control on the main thread.
    /// Nothing higher in the stack (reader, writer, commands) has to think about threads.
    /// </summary>
    public interface IWebSocket
    {
        IViewableDelegate<byte[]> Received { get; }

        /// <summary>
        /// The connection was lost not on our initiative: the server closed it, the network failed, a socket
        /// error. Invoked once with the close code and a reason for the log. Not invoked when the lifetime
        /// is terminated locally.
        /// </summary>
        IViewableDelegate<SocketCloseInfo> Closed { get; }

        UniTask Connect();
        // The bytes are the caller's again when the returned task completes (the writer sends from a buffer it
        // reuses): a socket that keeps them longer copies them.
        UniTask Send(ArraySegment<byte> bytes);

        /// <summary>
        /// The connection is found lost by this client (NetworkConnectionWatchdog: the server is silent): reported
        /// through <see cref="Closed"/> as a loss (1006 with the reason), and the socket is broken off without a
        /// leave, so the server sees a loss too. Nothing is invoked if the loss is already reported.
        /// </summary>
        void Drop(string reason);
    }
}