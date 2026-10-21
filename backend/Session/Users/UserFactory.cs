using System.Net.WebSockets;
using Common.Network;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Game.Session;

public interface IUserFactory
{
    // backendId: the meta id of the user (the one of its ticket); the user gets its id in this session (IUser.Id).
    IUser Create(IReadOnlyLifetime lifetime, Guid backendId, WebSocket webSocket);

    // A user played by the backend (IUser.IsBot), connected until the lifetime ends.
    IUser CreateBot(IReadOnlyLifetime lifetime, Guid backendId);
}

public class UserFactory : IUserFactory
{
    public UserFactory(
        ISessionUsers users,
        ISessionSeats seats,
        IExecutionQueue executionQueue,
        ICommandsCollection commandsCollection,
        ILogger<UserFactory> logger,
        ILogger<CommandDispatcher> dispatcherLogger)
    {
        _users = users;
        _seats = seats;
        _logger = logger;
        _dispatcherLogger = dispatcherLogger;
        _executionQueue = executionQueue;
        _commandsCollection = commandsCollection;
    }

    private readonly ISessionUsers _users;
    private readonly ISessionSeats _seats;
    private readonly ILogger<UserFactory> _logger;
    private readonly ILogger<CommandDispatcher> _dispatcherLogger;
    private readonly IExecutionQueue _executionQueue;
    private readonly ICommandsCollection _commandsCollection;

    public IUser Create(IReadOnlyLifetime parentLifetime, Guid backendId, WebSocket webSocket)
    {
        var connection = new Connection(webSocket, parentLifetime, _logger);
        var user = Create(parentLifetime, backendId, connection, false);

        _logger.LogInformation("[Session] User created: {Index} {UserId} {SessionUserId}, return: {IsReturn}",
            user.Index,
            user.BackendId,
            user.Id,
            user.Seat.IsReturn);

        return user;
    }

    public IUser CreateBot(IReadOnlyLifetime parentLifetime, Guid backendId)
    {
        var connection = new BotConnection
        {
            Lifetime = parentLifetime
        };

        var user = Create(parentLifetime, backendId, connection, true);

        _logger.LogInformation("[Session] Bot created: {Index} {UserId} {SessionUserId}", user.Index, user.BackendId, user.Id);

        return user;
    }

    private IUser Create(IReadOnlyLifetime parentLifetime, Guid backendId, IConnection connection, bool isBot)
    {
        // The seat of the user if it is still in the session (a reconnect: the same index), otherwise a new one.
        var seat = _seats.Take(backendId);
        var connectionLifetime = parentLifetime.Child();
        var userLifetime = new Lifetime();
        connectionLifetime.Listen(() => _executionQueue.Enqueue(userLifetime.Terminate));

        var dispatcher = new CommandDispatcher(_commandsCollection, _executionQueue, _dispatcherLogger);

        var user = new User
        {
            Seat = seat,
            IsBot = isBot,
            Lifetime = userLifetime,
            Dispatcher = dispatcher,
            Connection = connection
        };

        _seats.Attach(user);
        _users.AddUser(user);

        return user;
    }
}
