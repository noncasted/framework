namespace Game.Session;

// Admission rule of a session: who may still enter it and when it is closed.
// A session is closed once it has no joined users, no users in the middle of joining, no live
// reservations and no away seats. Bots (IUser.IsBot) are not counted: they never hold a session. The closed flag is set in the same critical section that counts users and reservations,
// so the search can never reserve a place in a session that is being closed.
// Synchronous, no timers inside: the caller passes the current time.
public interface ISessionAdmission
{
    bool IsClosed { get; }

    // No new reservations (TryReserve is refused, a live reservation is not refreshed either); the users that
    // hold a reservation still enter. A lobby that has started its match.
    bool IsSealed { get; }

    void Seal();

    // Search, lobby: reserves a place for SessionCreateOptions.JoinTimeout. A repeated call from the same user refreshes the reservation.
    // A user that reserves a place is not connected any more: its joined connections are stale (the client was lost
    // without a close; the session drops them, see ISession.DropConnections). They don't take a place, and a session
    // held only by them is refused - it is closed as soon as they are dropped.
    bool TryReserve(Guid userId, int maxUsers, DateTime now);

    // Not closed, not sealed and the user would get a place now (TryReserve would succeed).
    bool HasFreePlace(Guid userId, int maxUsers, DateTime now);

    // Gateway, on connect: the live reservation of the user turns into a joining user that keeps holding the
    // session (no entry without one - the limit and the seal are checked by TryReserve). False when the session is
    // closed or the user has no live reservation (never made, expired, already used by another connection).
    // allowsReturn (a session with a reconnect window): the user may also return to its seat without a reservation - a seat away
    // (OnUserAway, within its window) or a seat whose connection is still joined (the client has seen the loss before
    // the server; the old connection is dropped by the session). Neither the limit nor the seal apply: the place is
    // its own.
    bool TryAccept(Guid userId, DateTime now, bool allowsReturn);

    // The last connection of the user is lost without a leave in a session with a reconnect window. Its seat keeps
    // holding the session until now + ReconnectWindow; called before OnUserLeft of that connection.
    void OnUserAway(Guid userId, DateTime now);

    bool IsAway(Guid userId);

    // The window of the away seat is over: the seat is dropped here (the session ends the seat itself). False while
    // the window lasts or when the user is not away (it has returned).
    bool TryExpireAway(Guid userId, DateTime now);

    void OnUserJoined(Guid userId);

    // The user has at least one joined connection.
    bool IsJoined(Guid userId);

    // The following three return true when this very call has closed the session.
    bool OnUserLeft(Guid userId, DateTime now);
    bool OnJoinFailed(Guid userId, DateTime now);
    bool TryCloseIfEmpty(DateTime now);

    // Drops the reservation of the user, if it has one (the user took a place in another lobby). True when
    // this very call has closed the session.
    bool CancelReservation(Guid userId, DateTime now);

    // Users that hold a place without being joined: live reservations and users in the middle of joining.
    IReadOnlyList<Guid> GetPendingUsers(DateTime now);

    // Time until the last live reservation expires, null when there are none.
    TimeSpan? GetReservationsTimeout(DateTime now);
}

public class SessionAdmission : ISessionAdmission
{
    public SessionAdmission(SessionCreateOptions options)
    {
        _joinTimeout = options.JoinTimeout;
        _reconnectWindow = options.ReconnectWindow;
    }

    private readonly TimeSpan _joinTimeout;
    private readonly TimeSpan _reconnectWindow;

    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, DateTime> _reservations = new();
    private readonly Dictionary<Guid, int> _joining = new();

    // Away seats by user: the end of their reconnect window. Removed by a return or by TryExpireAway only.
    private readonly Dictionary<Guid, DateTime> _away = new();

    // Joined connections by user: a reconnecting user may hold two for a moment (the stale one is being dropped).
    private readonly Dictionary<Guid, int> _joined = new();

    private int _joiningCount;
    private int _joinedCount;
    private bool _isClosed;
    private bool _isSealed;

    public bool IsClosed
    {
        get
        {
            lock (_lock)
                return _isClosed;
        }
    }

    public bool IsSealed
    {
        get
        {
            lock (_lock)
                return _isSealed;
        }
    }

    public void Seal()
    {
        lock (_lock)
            _isSealed = true;
    }

    public bool TryReserve(Guid userId, int maxUsers, DateTime now)
    {
        lock (_lock)
        {
            if (_isClosed == true || _isSealed == true)
                return false;

            RemoveExpired(now);

            if (CanReserve(userId, maxUsers) == false)
                return false;

            _reservations[userId] = now + _joinTimeout;
            return true;
        }
    }

    public bool HasFreePlace(Guid userId, int maxUsers, DateTime now)
    {
        lock (_lock)
        {
            if (_isClosed == true || _isSealed == true)
                return false;

            RemoveExpired(now);

            return CanReserve(userId, maxUsers);
        }
    }

    public bool TryAccept(Guid userId, DateTime now, bool allowsReturn)
    {
        lock (_lock)
        {
            if (_isClosed == true)
                return false;

            RemoveExpired(now);

            var isReserved = _reservations.Remove(userId);
            var isReturn = allowsReturn == true && (_away.Remove(userId) == true || _joined.ContainsKey(userId) == true);

            if (isReserved == false && isReturn == false)
                return false;

            _joining[userId] = _joining.GetValueOrDefault(userId) + 1;
            _joiningCount++;

            return true;
        }
    }

    public void OnUserJoined(Guid userId)
    {
        lock (_lock)
        {
            if (_isClosed == true)
                throw new InvalidOperationException($"User {userId} joined a closed session");

            // A user may join without TryAccept (tests): then it only consumes its own reservation, if there is one.
            if (RemoveJoining(userId) == false)
                _reservations.Remove(userId);

            _joined[userId] = _joined.GetValueOrDefault(userId) + 1;
            _joinedCount++;
        }
    }

    public void OnUserAway(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_isClosed == true)
                throw new InvalidOperationException($"User {userId} is away from a closed session");

            if (_joined.ContainsKey(userId) == false)
                throw new InvalidOperationException($"User {userId} is away from a session it has not joined");

            if (_away.ContainsKey(userId) == true)
                throw new InvalidOperationException($"User {userId} is already away");

            if (_reconnectWindow == TimeSpan.Zero)
                throw new InvalidOperationException($"User {userId} is away from a session without a reconnect window");

            _away.Add(userId, now + _reconnectWindow);
        }
    }

    public bool IsAway(Guid userId)
    {
        lock (_lock)
            return _away.ContainsKey(userId);
    }

    public bool TryExpireAway(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_away.TryGetValue(userId, out var expiresAt) == false || expiresAt > now)
                return false;

            _away.Remove(userId);
            return true;
        }
    }

    public bool IsJoined(Guid userId)
    {
        lock (_lock)
            return _joined.ContainsKey(userId);
    }

    public bool OnUserLeft(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_joined.TryGetValue(userId, out var count) == false)
                throw new InvalidOperationException($"User {userId} left a session it has not joined");

            if (count == 1)
                _joined.Remove(userId);
            else
                _joined[userId] = count - 1;

            _joinedCount--;
            return CloseIfEmpty(now);
        }
    }

    public bool OnJoinFailed(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (RemoveJoining(userId) == false)
                throw new InvalidOperationException($"User {userId} is not joining the session");

            return CloseIfEmpty(now);
        }
    }

    public bool TryCloseIfEmpty(DateTime now)
    {
        lock (_lock)
            return CloseIfEmpty(now);
    }

    public bool CancelReservation(Guid userId, DateTime now)
    {
        lock (_lock)
        {
            if (_reservations.Remove(userId) == false)
                return false;

            return CloseIfEmpty(now);
        }
    }

    public IReadOnlyList<Guid> GetPendingUsers(DateTime now)
    {
        lock (_lock)
        {
            RemoveExpired(now);

            var users = new List<Guid>(_reservations.Keys);

            foreach (var userId in _joining.Keys)
            {
                if (users.Contains(userId) == false)
                    users.Add(userId);
            }

            return users;
        }
    }

    public TimeSpan? GetReservationsTimeout(DateTime now)
    {
        lock (_lock)
        {
            RemoveExpired(now);

            if (_reservations.Count == 0)
                return null;

            return _reservations.Values.Max() - now;
        }
    }

    // Not closed, not sealed, no expired reservations: checked by the caller.
    private bool CanReserve(Guid userId, int maxUsers)
    {
        // The joined connections of the user itself are stale: they neither take a place nor hold the session.
        var stale = _joined.GetValueOrDefault(userId);
        var awayOthers = _away.Count - (_away.ContainsKey(userId) == true ? 1 : 0);
        var holders = _joinedCount - stale + _joiningCount + _reservations.Count + awayOthers;

        if (stale != 0 && holders == 0)
            return false;

        return _reservations.ContainsKey(userId) == true || holders < maxUsers;
    }

    private bool CloseIfEmpty(DateTime now)
    {
        if (_isClosed == true)
            return false;

        RemoveExpired(now);

        if (_joinedCount != 0 || _joiningCount != 0 || _reservations.Count != 0 || _away.Count != 0)
            return false;

        _isClosed = true;
        return true;
    }

    private bool RemoveJoining(Guid userId)
    {
        if (_joining.TryGetValue(userId, out var count) == false)
            return false;

        if (count == 1)
            _joining.Remove(userId);
        else
            _joining[userId] = count - 1;

        _joiningCount--;
        return true;
    }

    private void RemoveExpired(DateTime now)
    {
        foreach (var (userId, expiresAt) in _reservations)
        {
            if (expiresAt <= now)
                _reservations.Remove(userId);
        }
    }
}
