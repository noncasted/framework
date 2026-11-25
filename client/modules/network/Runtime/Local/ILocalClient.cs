namespace Internal
{
    // The client side of a loopback socket, as the local server sees it.
    public interface ILocalClient
    {
        void Send(byte[] bytes);

        // The server closes the socket (a refused authorization): reported to the client as a loss with the code.
        void Close(SocketCloseInfo info);
    }
}
