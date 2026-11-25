using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Session;
using Shared;

namespace Internal
{
    public class LocalSession
    {
        public LocalSession(
            ILifetime lifetime,
            Guid id,
            SessionType type,
            int maxUsers,
            Func<SessionCore, IEnumerable<IService>> services,
            TimeSpan reconnectWindow)
        {
            _lifetime = lifetime;
            _maxUsers = maxUsers;
            _reconnectWindow = reconnectWindow;
            Id = id;

            Core = new SessionCore(id, type, lifetime, new UnitySessionLog(Logs.Of<SessionCore>()), reconnectWindow > TimeSpan.Zero);
            Core.SeatAway.Advise(lifetime, (seat, wait) => WaitReturn(seat, wait).NoAwait());

            foreach (var service in services(Core))
                Core.AddService(service);

            Core.Start();
        }

        // The seat of the first user: the indices of the backend start from 1.
        public const int FirstUserIndex = 1;

        private static readonly ILog _log = Logs.Of<LocalSession>();

        private readonly ILifetime _lifetime;
        private readonly int _maxUsers;
        private readonly TimeSpan _reconnectWindow;

        public Guid Id { get; }
        public SessionCore Core { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        // userId: the user of the ticket. A user with a seat here returns to it (a connection the host has not seen die
        // yet is stale and is dropped by the core, an away seat waits for it): the place is its own. A new user needs a
        // free place: every seat counts, an away one too, as in the admission of the backend.
        public SessionAuthStatus GetEntryStatus(Guid userId)
        {
            if (_lifetime.IsTerminated == true)
                return SessionAuthStatus.Closed;

            if (Core.Seats.Entries.ContainsKey(userId) == true)
                return SessionAuthStatus.Ok;

            // Every place is taken (the user of an offline session is already in it).
            if (Core.Seats.Entries.Count >= _maxUsers)
                return SessionAuthStatus.NoReservation;

            return SessionAuthStatus.Ok;
        }

        // backendId: the id of the user (the seat it returns to); index: the index of its seat (0 - the next one).
        public IUser Join(LocalSessionConnection connection, Guid backendId, int index)
        {
            var status = GetEntryStatus(backendId);

            if (status != SessionAuthStatus.Ok)
                throw new InvalidOperationException($"[Network] [Local] Session {Id} can't be entered: {status}");

            var user = Core.Connect(connection.Lifetime, backendId, connection, index);

            // The session ends once nobody is in it (the offline user has left, the host closes its match).
            user.Lifetime.Listen(() => {
                if (Core.Users.Count == 0)
                    _lifetime.Terminate();
            });

            _log.Info("User {Index} joins session {Id}", user.Index, Id);
            return user;
        }

        // The window in real time: the game of the host may stand meanwhile. The wait ends with the return of the user
        // or with the seat.
        private async UniTaskVoid WaitReturn(ISessionSeat seat, IReadOnlyLifetime wait)
        {
            var isCanceled = await UniTask
                                   .Delay(_reconnectWindow, DelayType.Realtime, cancellationToken: wait.Token)
                                   .SuppressCancellationThrow();

            if (isCanceled == true)
                return;

            Core.ExpireAway(seat);
        }
    }
}
