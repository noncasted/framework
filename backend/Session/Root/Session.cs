using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public interface ISession
{
    Guid Id { get; }
    SessionType Type { get; }

    // A private session is never returned by the search: the only way in is a reservation made by its
    // creator (lobby code, lobby start).
    bool IsPrivate { get; }

    // No place can be reserved or taken any more; the lifetime is terminated a moment later, from the queue.
    bool IsClosed { get; }

    // No new reservations: the lobby has started its match (see ISessionAdmission.IsSealed).
    bool IsSealed { get; }

    IReadOnlyLifetime Lifetime { get; }
    IUserFactory UserFactory { get; }
    ISessionUsers Users { get; }
    IExecutionQueue ExecutionQueue { get; }
    IViewableDelegate AllUsersConnected { get; }

    // Search, lobby: reserves a place for the user, false if the session is closed or full
    // (SessionCreateOptions.MaxUsers). The user comes anew: its connections still held here are stale and are
    // dropped (DropConnections); a session held only by them is refused (ISessionAdmission.TryReserve).
    bool TryReserve(Guid userId);

    // Any thread: the user comes anew (a search, a reservation), so its connections still held by this session belong
    // to a client lost without a close (a domain reload in the editor, a crash) - they are dropped in the queue.
    // A session left with nobody is closed as after any last disconnect.
    void DropConnections(Guid userId);

    // Any thread: the session is not closed and the user would get a place (joined, joining and reserved
    // users are counted, except the stale connections of the user itself).
    bool HasFreePlace(Guid userId);

    // Lobby: the user took a place in another lobby, its reservation here (if any) is dropped. A session
    // left with nobody is closed at once.
    void CancelReservation(Guid userId);

    // Gateway: false if the session is closed or the user holds no live reservation (ISessionAdmission.TryAccept);
    // in a session with a reconnect window the user may also return to its seat (away within its window, or still
    // joined). After true the caller must either create the user (in the execution queue) or report OnJoinFailed.
    bool TryAccept(Guid userId);
    void OnJoinFailed(Guid userId);

    Task Run();
}

public class SessionCreateOptions
{
    public required SessionType Type { get; init; }

    // The session starts handling its users once this many have joined (bots included); 0 - at once.
    public required int ExpectedUsers { get; init; }

    // Places of the users that hold the session (joined, joining, reserved, away); bots don't take one.
    public required int MaxUsers { get; init; }
    public required bool IsPrivate { get; init; }

    // Set by a creator that has to know the id before the session exists (a lobby that registers its code first).
    public Guid Id { get; init; } = Guid.NewGuid();

    // A reservation lives this long. A session that nobody enters is closed after it, and so is one whose expected
    // users have not all joined in this time (a player that never came must not hold it forever). Shorter than the
    // keep-alive abort of a connected socket that is not read yet.
    public TimeSpan JoinTimeout { get; init; } = TimeSpan.FromSeconds(30);

    // How long the seat of a user whose connection is lost (without SharedSessionPlayer.Leave) waits for its return:
    // its entities stay, the others get RemoteAway. Zero - no reconnect, a lost connection is a leave.
    public TimeSpan ReconnectWindow { get; init; } = TimeSpan.Zero;

    // The clock of the reservations and the windows.
    public TimeProvider Time { get; init; } = TimeProvider.System;
}

public class SessionContainerData
{
    public required int ExpectedUsers { get; init; }
    public required Guid Id { get; init; }
    public required ILifetime Lifetime { get; init; }
}

public class Session : ISession
{
    public Session(
        SessionContainerData data,
        SessionCreateOptions options,
        IUserFactory userFactory,
        ISessionUsers users,
        ISessionSeats seats,
        ISessionEntities entities,
        IExecutionQueue executionQueue,
        ISessionAdmission admission,
        TimeProvider time,
        ILogger<Session> logger)
    {
        _data = data;
        _options = options;
        _users = users;
        _seats = seats;
        _entities = entities;
        _admission = admission;
        _time = time;
        _logger = logger;
        ExecutionQueue = executionQueue;
        UserFactory = userFactory;

        _users.View(Lifetime, OnUserAdded);
    }

    private readonly ISessionUsers _users;
    private readonly ISessionSeats _seats;
    private readonly ISessionEntities _entities;
    private readonly ISessionAdmission _admission;
    private readonly TimeProvider _time;
    private readonly ILogger<Session> _logger;
    private readonly SessionContainerData _data;
    private readonly SessionCreateOptions _options;
    private readonly ViewableDelegate _allUsersConnected = new();

    public Guid Id => _data.Id;
    public SessionType Type => _options.Type;
    public bool IsPrivate => _options.IsPrivate;
    public bool IsClosed => _admission.IsClosed;
    public bool IsSealed => _admission.IsSealed;
    public IReadOnlyLifetime Lifetime => _data.Lifetime;

    public IUserFactory UserFactory { get; }
    public ISessionUsers Users => _users;
    public IExecutionQueue ExecutionQueue { get; }
    public IViewableDelegate AllUsersConnected => _allUsersConnected;

    public bool TryReserve(Guid userId)
    {
        DropConnections(userId);

        if (_admission.TryReserve(userId, _options.MaxUsers, GetNow()) == false)
            return false;

        ScheduleEmptyCheck(_options.JoinTimeout);
        return true;
    }

    public void DropConnections(Guid userId)
    {
        if (_admission.IsJoined(userId) == false)
            return;

        // FIFO: the drop runs before any connection the user opens after this call. A user that comes anew has left
        // its seat here: no reconnect window.
        ExecutionQueue.Enqueue(() => {
            foreach (var user in _users.Where(user => user.IsBot == false && user.BackendId == userId).ToList())
            {
                if (user.Seat.Connected == user)
                    user.Seat.MarkLeaving(user);

                Disconnect(user, "the user came anew");
            }
        });
    }

    public bool HasFreePlace(Guid userId)
    {
        return _admission.HasFreePlace(userId, _options.MaxUsers, GetNow());
    }

    public void CancelReservation(Guid userId)
    {
        if (_admission.CancelReservation(userId, GetNow()) == true)
            Close();
    }

    public bool TryAccept(Guid userId)
    {
        return _admission.TryAccept(userId, GetNow(), IsReconnectable);
    }

    public void OnJoinFailed(Guid userId)
    {
        if (_admission.OnJoinFailed(userId, GetNow()) == true)
        {
            Close();
            return;
        }

        // A returning user that has not made it: its seat has no connection and is not away any more (TryAccept took
        // it), nothing would end it. It leaves.
        ExecutionQueue.Enqueue(() => {
            if (Lifetime.IsTerminated == true || _seats.Entries.TryGetValue(userId, out var seat) == false)
                return;

            if (seat.Connected != null || _admission.IsAway(userId) == true)
                return;

            _logger.LogWarning("[Session] Session {ID}: user {Index} {UserId} failed to return to its seat: it leaves",
                Id,
                seat.Index,
                userId);

            EndSeat(seat);
        });
    }

    private bool IsReconnectable => _options.ReconnectWindow > TimeSpan.Zero;

    // Completes when the users are handled (a session with ExpectedUsers - once all of them have joined) or when the
    // session ends before that.
    public Task Run()
    {
        // A session that nobody reserves or enters (Create with ExpectedUsers) is closed after JoinTimeout.
        ScheduleEmptyCheck(_options.JoinTimeout);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Lifetime.Listen(() => started.TrySetResult());

        // Users are added and removed only in the queue: the wait and the handling are set up there too.
        ExecutionQueue.Enqueue(() => AwaitUsersJoin(started));

        return started.Task;
    }

    // Runs in the execution queue. The expected users are awaited by the joins themselves, not by a poll: the last of
    // them starts the handling. A user that joins after it (a second connection) is handled too. Not all of them in
    // JoinTimeout - the session closes.
    private void AwaitUsersJoin(TaskCompletionSource started)
    {
        if (Lifetime.IsTerminated == true)
            return;

        if (_data.ExpectedUsers == 0)
        {
            Start();
            return;
        }

        var waiting = Lifetime.Child();

        _users.View(waiting, _ => {
            if (_users.Count < _data.ExpectedUsers)
                return;

            // Not in place: the listeners of this addition run on the current list first.
            ExecutionQueue.Enqueue(() => {
                // Already started, or a user left in between: the next join checks again.
                if (waiting.IsTerminated == true || _users.Count < _data.ExpectedUsers)
                    return;

                waiting.Terminate();
                Start();
            });
        });

        CloseIfNotJoined().NoAwait();

        async Task CloseIfNotJoined()
        {
            await Task.Delay(_options.JoinTimeout, _time).ConfigureAwait(false);

            ExecutionQueue.Enqueue(() => {
                if (waiting.IsTerminated == true)
                    return;

                waiting.Terminate();

                _logger.LogWarning("[Session] Session {ID}: {Count} of {Expected} users joined in {Timeout}, closing",
                    Id,
                    _users.Count,
                    _data.ExpectedUsers,
                    _options.JoinTimeout);

                _data.Lifetime.Terminate();
            });
        }

        void Start()
        {
            _users.View(Lifetime, user => HandleUser(user).NoAwait());
            _allUsersConnected.Invoke();
            started.TrySetResult();
        }
    }

    // Runs in the execution queue (users are added and removed only there).
    private void OnUserAdded(IReadOnlyLifetime userLifetime, IUser user)
    {
        // A bot is connected as long as the session and never holds it.
        if (user.IsBot == true)
        {
            userLifetime.Listen(() => _seats.Detach(user));
            return;
        }

        _admission.OnUserJoined(user.BackendId);

        userLifetime.Listen(() => OnConnectionEnded(user));

        // A user connects once: an older connection of the same user is stale (its client was lost without a close
        // and the reservation did not drop it in time). Dropped after the listeners of this addition.
        var stale = _users.Where(other => other != user && other.BackendId == user.BackendId).ToList();

        if (stale.Count == 0)
            return;

        ExecutionQueue.Enqueue(() => {
            foreach (var other in stale)
                Disconnect(other, "the user connected again");
        });
    }

    // Runs in the queue: the connection of the user is removed from the users (its lifetime has ended).
    private void OnConnectionEnded(IUser user)
    {
        var isCurrent = _seats.Detach(user);
        var now = GetNow();

        // A connection replaced by a newer one of the same seat (a return before its loss was noticed) ends silently;
        // the end of the session ends every seat by itself.
        if (isCurrent == true && Lifetime.IsTerminated == false)
        {
            var seat = user.Seat;

            if (seat.IsLeaving == true || IsReconnectable == false)
            {
                EndSeat(seat);
            }
            else
            {
                // Before OnUserLeft: the away seat keeps holding the session.
                _admission.OnUserAway(seat.UserId, now);

                _logger.LogInformation(
                    "[Session] Session {ID}: user {Index} {UserId} lost its connection, its seat waits {Window} s",
                    Id,
                    seat.Index,
                    seat.UserId,
                    _options.ReconnectWindow.TotalSeconds);

                _users.SendAll(new SharedSessionPlayer.RemoteAway()
                {
                    Index = seat.Index
                });

                ScheduleAwayExpiry(seat, _options.ReconnectWindow);
            }
        }

        if (_admission.OnUserLeft(user.BackendId, now) == true)
            Close();
    }

    // The user is gone for good: the others destroy its entities with it (the backend ends them with the seat).
    private void EndSeat(ISessionSeat seat)
    {
        _users.SendAll(new SharedSessionPlayer.RemoteDisconnect()
        {
            Index = seat.Index
        });

        _seats.End(seat);
    }

    private void ScheduleAwayExpiry(ISessionSeat seat, TimeSpan delay)
    {
        Check().NoAwait();

        async Task Check()
        {
            await Task.Delay(delay, _time).ConfigureAwait(false);
            ExecutionQueue.Enqueue(() => ExpireAway(seat));
        }
    }

    private void ExpireAway(ISessionSeat seat)
    {
        if (Lifetime.IsTerminated == true || seat.Lifetime.IsTerminated == true)
            return;

        // Returned (or returning: TryAccept has taken the away mark).
        if (_admission.IsAway(seat.UserId) == false)
            return;

        // The timer and the clock are different sources: one more check.
        if (_admission.TryExpireAway(seat.UserId, GetNow()) == false)
        {
            ScheduleAwayExpiry(seat, TimeSpan.FromSeconds(1));
            return;
        }

        if (seat.Connected != null)
            throw new InvalidOperationException($"Seat {seat.Index} {seat.UserId} is away with a connection");

        _logger.LogInformation("[Session] Session {ID}: user {Index} {UserId} has not returned in time: it leaves",
            Id,
            seat.Index,
            seat.UserId);

        EndSeat(seat);

        if (_admission.TryCloseIfEmpty(GetNow()) == true)
            Close();
    }

    // The connection is closed; the user is removed as after any disconnect (HandleUser).
    private void Disconnect(IUser user, string reason)
    {
        _logger.LogInformation("[Session] Session {ID}: stale connection of user {Index} {UserId} dropped: {Reason}",
            Id,
            user.Index,
            user.BackendId,
            reason);

        user.Connection.ForceDisconnect();
    }

    // The session is already marked closed in the admission, so neither the search nor the gateway can
    // pick it. Termination is enqueued instead of being called in place: the caller may be in the middle
    // of a user removal, the other listeners of that removal must finish on a live session.
    private void Close()
    {
        _logger.LogInformation("[Session] Session {ID} closed: no users", Id);
        ExecutionQueue.Enqueue(_data.Lifetime.Terminate);
    }

    private void ScheduleEmptyCheck(TimeSpan delay)
    {
        Check().NoAwait();

        async Task Check()
        {
            await Task.Delay(delay, _time).ConfigureAwait(false);
            ExecutionQueue.Enqueue(CheckEmpty);
        }
    }

    private void CheckEmpty()
    {
        if (_admission.IsClosed == true)
            return;

        var now = GetNow();

        if (_admission.TryCloseIfEmpty(now) == true)
        {
            Close();
            return;
        }

        // The timer and the clock are different sources: a reservation that is still alive gets one more check.
        var timeout = _admission.GetReservationsTimeout(now);

        if (timeout != null)
            ScheduleEmptyCheck(timeout.Value);
    }

    private DateTime GetNow()
    {
        return _time.GetUtcNow().UtcDateTime;
    }

    private async Task HandleUser(IUser user)
    {
        await HandleUserConnect(user);
        ExecutionQueue.Enqueue(() => HandleUserDisconnect(user));
    }

    private Task HandleUserConnect(IUser user)
    {
        user.Dispatcher.Run(user.Lifetime, user);

        var connectionTask = user.Connection.Run();

        // The entities of the seat to restore: the ones its client has created (the ones built by the backend come
        // to it as remote ones).
        user.Send(new SharedSessionPlayer.LocalUpdate()
        {
            Index = user.Index,
            Id = user.Id,
            EntityCounter = user.Seat.EntityCounter,
            IsReturn = user.Seat.IsReturn,
            SeatEntities = _entities.Entries.Values.Count(entity =>
                entity.Owner == user.Seat && SharedEntityIds.IsBackendIssued(entity.Id) == false)
        });

        _users.IterateOthers(user, other => {
            // Peers know each other by the index and the id in this session only: the meta id of a user is its login.
            user.Send(new SharedSessionPlayer.RemoteUpdate()
            {
                Index = other.Index,
                Id = other.Id
            });

            other.Send(new SharedSessionPlayer.RemoteUpdate()
            {
                Index = user.Index,
                Id = user.Id
            });
        });

        foreach (var (_, entity) in _entities.Entries)
            user.Send(entity.CreateOverview());

        return connectionTask;
    }

    // What the others are told depends on the seat (OnConnectionEnded): a leave, a loss or nothing.
    private void HandleUserDisconnect(IUser user)
    {
        ExecutionQueue.Enqueue(user.Lifetime.Terminate);
    }
}