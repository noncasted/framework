using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Session;
using Shared;
using UnityEngine;

namespace Internal
{
    // A game server inside the client: the sessions of the offline mode and of a P2P host. A session is the session
    // core of the backend (SessionCore, shared/SessionCore) with the services of its creator; the client enters it by
    // its ticket, as with the game gateway.
    public class LocalSessionServer : ILocalServer
    {
        public LocalSessionServer(IReadOnlyLifetime lifetime, string url)
        {
            _lifetime = lifetime;
            Url = url;
        }

        private const string TicketPrefix = "offline.";

        private readonly IReadOnlyLifetime _lifetime;
        private readonly Dictionary<Guid, LocalSession> _sessions = new();

        public string Url { get; }

        // services: the game logic of the session (the IService the backend registers for its type), built over the
        // parts of the core (owner, users, log).
        public LocalSession Create(SessionType type, int maxUsers, Func<SessionCore, IEnumerable<IService>> services)
        {
            return Create(Guid.NewGuid(), type, maxUsers, services);
        }

        // id: given by the creator (a P2P host runs the session of the match under the id of the server session).
        public LocalSession Create(Guid id, SessionType type, int maxUsers, Func<SessionCore, IEnumerable<IService>> services)
        {
            var session = new LocalSession(_lifetime.Child(), id, type, maxUsers, services);

            _sessions.Add(session.Id, session);
            session.Lifetime.Listen(() => _sessions.Remove(session.Id));

            return session;
        }

        // The ticket of a user: the session, the id of the user (its seat) and the index of the seat (0 - the next
        // one). The player of this device by default; a P2P host gives each player its seat of the server session.
        public string GetTicket(Guid sessionId)
        {
            return GetTicket(sessionId, OfflineUserId, 0);
        }

        public string GetTicket(Guid sessionId, Guid userId, int index)
        {
            if (_sessions.ContainsKey(sessionId) == false)
                throw new LocalServerRequestException(ErrorCode.NotFound, $"Session {sessionId} is not on the local server");

            return CreateTicket(sessionId, userId, index);
        }

        // A ticket of a session that may not exist yet (a guest of a P2P host builds it from what the server session
        // tells it).
        public static string CreateTicket(Guid sessionId, Guid userId, int index)
        {
            return $"{TicketPrefix}{sessionId:N}.{userId:N}.{index}";
        }

        public ILocalServerConnection Accept(IReadOnlyLifetime lifetime, ILocalClient client)
        {
            return new LocalSessionConnection(lifetime, client, this);
        }

        // A ticket of another format or of a session that is not here: no session.
        public LocalSession FindByTicket(string ticket, out Guid userId, out int index)
        {
            userId = Guid.Empty;
            index = 0;

            if (ticket.StartsWith(TicketPrefix, StringComparison.Ordinal) == false)
                return null;

            var parts = ticket.Substring(TicketPrefix.Length).Split('.');

            if (parts.Length != 3 ||
                Guid.TryParseExact(parts[0], "N", out var id) == false ||
                Guid.TryParseExact(parts[1], "N", out userId) == false ||
                int.TryParse(parts[2], out index) == false)
                return null;

            return _sessions.TryGetValue(id, out var session) == true ? session : null;
        }

        // A socket of another process to this server (a guest of a P2P host over a data channel): its frames go to a
        // connection of the server as the frames of a loopback socket. The lifetime ends with the socket.
        public void AcceptSocket(IReadOnlyLifetime lifetime, IWebSocket socket)
        {
            var connectionLifetime = lifetime.Child();
            var connection = Accept(connectionLifetime, new SocketClient(socket, connectionLifetime));

            socket.Received.Advise(connectionLifetime, connection.OnReceived);
            socket.Closed.Advise(connectionLifetime, _ => connectionLifetime.Terminate());
        }

        // The meta id of the player of this device in a local session.
        private static Guid OfflineUserId => new("00000000-0000-0000-0000-00000000000f");

        private class SocketClient : ILocalClient
        {
            public SocketClient(IWebSocket socket, ILifetime lifetime)
            {
                _socket = socket;
                _lifetime = lifetime;
            }

            private readonly IWebSocket _socket;
            private readonly ILifetime _lifetime;

            public void Send(byte[] bytes)
            {
                _socket.Send(bytes).Forget();
            }

            // The frames sent before the close are queued on the socket already; the peer sees the loss of the
            // channel (there is no close code on a data channel). The socket ends too: a tunnel left open would take
            // the next try of the guest into a connection nobody reads.
            public void Close(SocketCloseInfo info)
            {
                Debug.Log($"[Network] [Local] The server closes a socket: {info}");
                _lifetime.Terminate();
                _socket.Drop($"closed by the server: {info}");
            }
        }
    }

    public class LocalSession
    {
        public LocalSession(
            ILifetime lifetime,
            Guid id,
            SessionType type,
            int maxUsers,
            Func<SessionCore, IEnumerable<IService>> services)
        {
            _lifetime = lifetime;
            _maxUsers = maxUsers;
            Id = id;

            Core = new SessionCore(id, type, lifetime, new UnitySessionLog());

            foreach (var service in services(Core))
                Core.AddService(service);

            Core.Start();
        }

        // The seat of the first user: the indices of the backend start from 1.
        public const int FirstUserIndex = 1;

        private readonly ILifetime _lifetime;
        private readonly int _maxUsers;

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
    }

    public class LocalSessionConnection : LocalServerConnection, IUserConnection
    {
        public LocalSessionConnection(IReadOnlyLifetime lifetime, ILocalClient client, LocalSessionServer server)
            : base(lifetime, client)
        {
            _server = server;
        }

        private readonly LocalSessionServer _server;

        private LocalSession _session;
        private IUser _user;

        void IUserConnection.Respond(INetworkContext context, int requestId)
        {
            Respond(requestId, context);
        }

        void IUserConnection.Disconnect()
        {
            Close(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, "dropped by the session"));
        }

        protected override void OnRequest(int requestId, INetworkContext context)
        {
            if (context is SharedSessionAuth.Request auth)
            {
                Authorize(requestId, auth);
                return;
            }

            _session.Core.OnRequest(GetUser(), context, requestId);
        }

        protected override void OnOneWay(INetworkContext context)
        {
            _session.Core.OnOneWay(GetUser(), context);
        }

        private void Authorize(int requestId, SharedSessionAuth.Request auth)
        {
            if (_user != null)
                throw new LocalServerRequestException(ErrorCode.InvalidRequest, "The connection is already authorized");

            var session = _server.FindByTicket(auth.Ticket, out var userId, out var index);
            var status = session == null ? SessionAuthStatus.InvalidTicket : session.GetEntryStatus();

            // The response goes before LocalUpdate, as from the game gateway.
            Respond(requestId, new SharedSessionAuth.Response { Status = status });

            if (status != SessionAuthStatus.Ok)
            {
                Close(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"session entry refused: {status}"));
                return;
            }

            _session = session;
            _user = session.Join(this, userId, index);
        }

        private IUser GetUser()
        {
            if (_user == null)
                throw new InvalidOperationException("[Network] [Local] A message before the session authorization");

            return _user;
        }
    }

    // The log of a session core inside the client.
    public class UnitySessionLog : ISessionLog
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
            UnityEngine.Debug.Log($"[Network] [Local] {message}");
        }

        public void Warning(string message)
        {
            UnityEngine.Debug.LogWarning($"[Network] [Local] {message}");
        }

        public void Error(string message, Exception exception = null)
        {
            UnityEngine.Debug.LogError($"[Network] [Local] {message}");

            if (exception != null)
                UnityEngine.Debug.LogException(exception);
        }
    }
}
