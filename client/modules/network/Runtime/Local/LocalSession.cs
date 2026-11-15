using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Session;
using Shared;
using UnityEngine;

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

            Core = new SessionCore(id, type, lifetime, new UnitySessionLog(), reconnectWindow > TimeSpan.Zero);
            Core.SeatAway.Advise(lifetime, (seat, wait) => WaitReturn(seat, wait).Forget());

            foreach (var service in services(Core))
                Core.AddService(service);

            Core.Start();
        }

        // The seat of the first user: the indices of the backend start from 1.
        public const int FirstUserIndex = 1;

        private readonly ILifetime _lifetime;
        private readonly int _maxUsers;
        private readonly TimeSpan _reconnectWindow;

        public Guid Id { get; }
        public SessionCore Core { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public SessionAuthStatus GetEntryStatus()
        {
            if (_lifetime.IsTerminated == true)
                return SessionAuthStatus.Closed;

            // Every place is taken (the user of an offline session is already in it).
            if (Core.Users.Count >= _maxUsers)
                return SessionAuthStatus.NoReservation;

            return SessionAuthStatus.Ok;
        }

        // backendId: the id of the user (the seat it returns to); index: the index of its seat (0 - the next one).
        public IUser Join(LocalSessionConnection connection, Guid backendId, int index)
        {
            if (GetEntryStatus() != SessionAuthStatus.Ok)
                throw new InvalidOperationException($"[Network] [Local] Session {Id} can't be entered: {GetEntryStatus()}");

            var user = Core.Connect(connection.Lifetime, backendId, connection, index);

            // The session ends once nobody is in it (the offline user has left, the host closes its match).
            user.Lifetime.Listen(() => {
                if (Core.Users.Count == 0)
                    _lifetime.Terminate();
            });

            Debug.Log($"[Network] [Local] User {user.Index} joins session {Id}");
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
