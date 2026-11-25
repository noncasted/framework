using Internal;

namespace Global.Lobbies
{
    // The socket of the meta the requests go through: the game registers its meta backend as this (the connection
    // is the current one, a reconnected socket is a new connection).
    public interface IMetaSocket
    {
        INetworkConnection Connection { get; }
    }
}
