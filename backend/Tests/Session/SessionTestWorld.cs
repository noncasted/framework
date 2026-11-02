using Common.Network;
using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.Session;

// One real session container (AddSessionServices) on a manual execution queue and manual time: no threads and no
// real delays. A user is created by the real UserFactory, over a TestConnection instead of a WebSocket;
// what the game gateway does (TryAccept, then the creation in the queue) is Enter. A connection ends through
// IConnection.Run, so a user leaves the session the way it does after a real disconnect (SessionUserRuns.Run).
public class TestSession
{
    // configure: what the host of a session of a game adds to its container (the services of the session, the
    // registrations of the lobbies module); it runs after AddSessionServices and before the queue override.
    public TestSession(SessionCreateOptions options, Action<IServiceCollection>? configure = null)
    {
        Options = options;
        Time = (ManualTimeProvider)options.Time;

        var data = new SessionContainerData
        {
            Id = options.Id,
            Lifetime = Lifetime,
            ExpectedUsers = options.ExpectedUsers
        };

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSessionServices(data, options);
        configure?.Invoke(services);

        // The last registration wins: the queue is pumped by the test.
        services.AddSingleton<IExecutionQueue>(Queue);

        Provider = services.BuildServiceProvider();

        Session = Provider.GetRequiredService<ISession>();
        Admission = Provider.GetRequiredService<ISessionAdmission>();
        Users = Provider.GetRequiredService<ISessionUsers>();
        Seats = Provider.GetRequiredService<ISessionSeats>();
        Entities = Provider.GetRequiredService<ISessionEntities>();
        Objects = Provider.GetRequiredService<ISessionObjects>();
        EntityFactory = Provider.GetRequiredService<IEntityFactory>();
        ServiceFactory = Provider.GetRequiredService<IServiceFactory>();
        Commands = Provider.GetRequiredService<ICommandsCollection>();

        // The owner follows the users from its creation: created before any user joins.
        Owner = Provider.GetRequiredService<ISessionOwner>();
    }

    public readonly Lifetime Lifetime = new();
    public readonly ManualExecutionQueue Queue = new();
    public readonly ManualTimeProvider Time;
    public readonly SessionCreateOptions Options;
    public readonly ServiceProvider Provider;
    public readonly ISession Session;
    public readonly ISessionAdmission Admission;
    public readonly ISessionUsers Users;
    public readonly ISessionSeats Seats;
    public readonly ISessionEntities Entities;
    public readonly ISessionObjects Objects;
    public readonly IEntityFactory EntityFactory;
    public readonly IServiceFactory ServiceFactory;
    public readonly ICommandsCollection Commands;
    public readonly ISessionOwner Owner;

    public Task RunTask { get; private set; } = Task.CompletedTask;

    public static TestSession Create(
        int expectedUsers = 0,
        int maxUsers = 4,
        SessionType type = SessionType.Match,
        TimeSpan? reconnectWindow = null,
        TimeSpan? joinTimeout = null,
        bool run = true)
    {
        var options = new SessionCreateOptions
        {
            Type = type,
            ExpectedUsers = expectedUsers,
            MaxUsers = maxUsers,
            IsPrivate = false,
            ReconnectWindow = reconnectWindow ?? TimeSpan.Zero,
            JoinTimeout = joinTimeout ?? TimeSpan.FromSeconds(30),
            Time = new ManualTimeProvider()
        };

        var session = new TestSession(options);

        if (run == true)
            session.Run();

        return session;
    }

    public Task Run()
    {
        RunTask = Session.Run();
        Queue.Drain();
        return RunTask;
    }

    // The search / the lobby: a place reserved for the user.
    public bool Reserve(Guid backendId)
    {
        return Session.TryReserve(backendId);
    }

    // The gateway: the place check, then the user is created in the queue. Null is the refusal.
    public IUser? Enter(Guid backendId)
    {
        if (Session.TryAccept(backendId) == false)
            return null;

        return Join(backendId);
    }

    // A reservation and the entry right after it: a new user of the session.
    public IUser ReserveAndEnter(Guid backendId)
    {
        if (Reserve(backendId) == false)
            throw new InvalidOperationException($"User {backendId} got no reservation");

        return Enter(backendId) ?? throw new InvalidOperationException($"User {backendId} was not accepted");
    }

    public IUser ReserveAndEnter()
    {
        return ReserveAndEnter(Guid.NewGuid());
    }

    // What the gateway does in the session queue after TryAccept: the real UserFactory.Create, over a TestConnection
    // instead of a WebSocket. connection: a connection built by the test (over Session.Lifetime), e.g. a failing one.
    public IUser Join(Guid backendId, TestConnection? connection = null)
    {
        IUser? created = null;
        EnqueueJoin(backendId, user => created = user, connection);
        Queue.Drain();

        return created ?? throw new InvalidOperationException($"User {backendId} was not created");
    }

    // The creation of the user is only queued: the test decides what runs before it.
    public void EnqueueJoin(Guid backendId, Action<IUser>? onCreated = null, TestConnection? connection = null)
    {
        Queue.Enqueue(() => {
            var user = Session.UserFactory.Create(Session.Lifetime, backendId, connection ?? new TestConnection(Session.Lifetime));
            onCreated?.Invoke(user);
        });
    }

    // A bot of the session (the real UserFactory.CreateBot), connected until the session ends.
    public IUser AddBot()
    {
        IUser? created = null;
        Queue.Enqueue(() => created = Session.UserFactory.CreateBot(Session.Lifetime, Guid.NewGuid()));
        Queue.Drain();

        return created ?? throw new InvalidOperationException("The bot was not created");
    }

    // The client leaves on purpose: SharedSessionPlayer.Leave, then the socket closes.
    public void Leave(IUser user)
    {
        var connection = Connection(user);
        connection.ReceiveOneWay(new SharedSessionPlayer.Leave());
        connection.Close();
        Queue.Drain();

        if (user.Lifetime.IsTerminated == false)
            throw new InvalidOperationException($"User {user.Index} has not left: is the session handling its users?");
    }

    // The socket is lost without a leave.
    public void Lose(IUser user)
    {
        Connection(user).Close();
        Queue.Drain();

        if (user.Lifetime.IsTerminated == false)
            throw new InvalidOperationException($"User {user.Index} is still connected: is the session handling its users?");
    }

    public void Advance(TimeSpan delta)
    {
        Time.Advance(delta);
        Queue.Drain();
    }

    // A one-way command of the client, executed directly (a failure throws to the test instead of being logged).
    public void Execute(IUser user, INetworkContext context)
    {
        Commands.OneWay[context.GetType()].Execute(user, context);
    }

    public INetworkContext Request(IUser user, INetworkContext context)
    {
        return Commands.Responsible[context.GetType()].Execute(user, context);
    }

    public static TestConnection Connection(IUser user)
    {
        return (TestConnection)((IHostedUser)user).Socket;
    }

    public static IReadOnlyList<INetworkContext> Sent(IUser user)
    {
        return Connection(user).Sent;
    }

    public static IReadOnlyList<T> Sent<T>(IUser user)
    {
        return Connection(user).Sent.OfType<T>().ToList();
    }

    public void ClearSent()
    {
        foreach (var user in Users)
        {
            if (user.IsBot == false)
                Connection(user).ClearSent();
        }
    }
}

// The server side of a client connection: records what the session sends, delivers what the test makes the client
// send. Like Common.Network.Connection: its lifetime is a child of the session one, Run completes when the connection
// is closed (by the client, forced by the session, or with the session) and fails on a connection terminated before it.
public class TestConnection : IConnection, IConnectionReader, IConnectionWriter
{
    public TestConnection(IReadOnlyLifetime parentLifetime)
    {
        _lifetime = parentLifetime.Child();
        _lifetime.Listen(() => _run.TrySetResult());
    }

    private readonly TaskCompletionSource _run = new();
    private readonly ILifetime _lifetime;
    private readonly List<INetworkContext> _sent = new();
    private readonly ViewableDelegate<OneWayMessageFromClient> _oneWay = new();
    private readonly ViewableDelegate<RequestMessageFromClient> _requests = new();
    private readonly ViewableDelegate<ResponseMessageFromClient> _responses = new();

    public IReadOnlyLifetime Lifetime => _lifetime;
    public IConnectionReader Reader => this;
    public IConnectionWriter Writer => this;
    public IViewableDelegate<OneWayMessageFromClient> OneWay => _oneWay;
    public IViewableDelegate<RequestMessageFromClient> Requests => _requests;
    public IViewableDelegate<ResponseMessageFromClient> Responses => _responses;

    public int ForceDisconnects { get; private set; }

    public IReadOnlyList<INetworkContext> Sent
    {
        get
        {
            lock (_sent)
                return _sent.ToList();
        }
    }

    // Not null: Run fails with it (a transport error of the socket).
    public Exception? RunFailure { get; init; }

    public Task Run()
    {
        if (_lifetime.IsTerminated == true)
            return Task.FromException(new InvalidOperationException("Connection is terminated"));

        if (RunFailure != null)
            return Task.FromException(RunFailure);

        return _run.Task;
    }

    public void ForceDisconnect()
    {
        ForceDisconnects++;
        Close();
    }

    public void Close()
    {
        _lifetime.Terminate();
    }

    public void ReceiveOneWay(INetworkContext context)
    {
        _oneWay.Invoke(new OneWayMessageFromClient
        {
            Context = context
        });
    }

    public void ClearSent()
    {
        lock (_sent)
            _sent.Clear();
    }

    public void WriteOneWay(INetworkContext context)
    {
        lock (_sent)
            _sent.Add(context);
    }

    public Task<T> WriteRequest<T>(INetworkContext context) where T : INetworkContext
    {
        throw new NotSupportedException("The session sends no requests in these tests");
    }

    public void WriteResponse(INetworkContext context, int requestId)
    {
        lock (_sent)
            _sent.Add(context);
    }

    public void OnRequestHandled(INetworkContext context, int requestId)
    {
    }
}

// Single consumer at a time, like the real ExecutionQueue, but pumped by the test. Actions run without a
// synchronization context: an await inside the session (SessionUserRuns.Run) continues inline when its task completes,
// not on a context of the test runner.
public class ManualExecutionQueue : IExecutionQueue
{
    private readonly Queue<Action> _actions = new();
    private readonly Lock _runLock = new();

    public int Count
    {
        get
        {
            lock (_actions)
                return _actions.Count;
        }
    }

    public void Enqueue(Action action)
    {
        lock (_actions)
            _actions.Enqueue(action);
    }

    public bool RunNext()
    {
        lock (_runLock)
        {
            Action? action;

            lock (_actions)
            {
                if (_actions.TryDequeue(out action) == false)
                    return false;
            }

            var context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                action();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }

            return true;
        }
    }

    public void Drain()
    {
        while (RunNext() == true)
        {
        }
    }
}

public class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private int _createdTimers;

    public int CreatedTimers
    {
        get
        {
            lock (_timers)
                return _createdTimers;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_timers)
            return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_timers)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            _createdTimers++;
            return timer;
        }
    }

    // Fires the timers that are due, in the order of their due time.
    public void Advance(TimeSpan delta)
    {
        List<ManualTimer> due;

        lock (_timers)
        {
            _now += delta;
            due = _timers.Where(timer => timer.DueAt <= _now).OrderBy(timer => timer.DueAt).ToList();
            _timers.RemoveAll(timer => timer.DueAt <= _now);
        }

        foreach (var timer in due)
            timer.Fire();
    }
}

public class ManualTimer : ITimer
{
    public ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt)
    {
        _callback = callback;
        _state = state;
        DueAt = dueAt;
    }

    private readonly TimerCallback _callback;
    private readonly object? _state;
    private bool _isDisposed;

    public DateTimeOffset DueAt { get; }

    public void Fire()
    {
        if (_isDisposed == false)
            _callback(_state);
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

public class TestPayload : IEntityPayload
{
}

public class TestSessionPayload : ISessionEntityPayload
{
}
