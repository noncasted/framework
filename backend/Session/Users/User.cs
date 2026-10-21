using Common.Network;
using Common.Reactive;
using Shared;

namespace Game.Session;

// One connection of a user to the session. The place of the user (index, entities) is its seat, kept across
// connections: Index is the index of the seat.
public interface IUser
{
    // The id of the user in this session (of its seat): a new one in every session, the one the game and its clients
    // know (SharedSessionPlayer.LocalUpdate / RemoteUpdate). BackendId is the meta id - the login of the user: only for
    // the meta and the logs, never sent to the clients.
    Guid Id { get; }
    Guid BackendId { get; }
    int Index { get; }

    // A user played by the backend: no client, its connection lives as long as the session and it never holds the
    // session open (ISessionAdmission does not count it).
    bool IsBot { get; }

    ISessionSeat Seat { get; }
    ILifetime Lifetime { get; }
    IConnection Connection { get; }
    ICommandDispatcher Dispatcher { get; }
}

public class User : IUser
{
    public required ISessionSeat Seat { get; init; }
    public Guid Id => Seat.Id;
    public Guid BackendId => Seat.UserId;
    public int Index => Seat.Index;
    public required bool IsBot { get; init; }
    public required ILifetime Lifetime { get; init; }
    public required IConnection Connection { get; init; }
    public required ICommandDispatcher Dispatcher { get; init; }
}

public static class UserExtensions
{
    extension(IUser user)
    {
        public void Send(INetworkContext context)
        {
            user.Connection.Writer.WriteOneWay(context);
        }

        public void Send(INetworkContext context, int requestId)
        {
            user.Connection.Writer.WriteResponse(context, requestId);
        }
    }
}
