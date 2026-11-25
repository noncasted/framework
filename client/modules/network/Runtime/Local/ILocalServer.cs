namespace Internal
{
    // A server that runs inside the client process (the offline mode): it is addressed by a url of the scheme
    // LocalServerUrl.Scheme, NetworkConnection opens a LoopbackWebSocket to it instead of a network socket. The
    // client stack above the socket (reader, writer, commands, sessions) works the same as with a remote server.
    public interface ILocalServer
    {
        // A socket of the client is opened: the returned connection gets every frame the client sends, the client
        // gets every frame the connection sends to it. The lifetime ends with the socket.
        ILocalServerConnection Accept(IReadOnlyLifetime lifetime, ILocalClient client);
    }
}
