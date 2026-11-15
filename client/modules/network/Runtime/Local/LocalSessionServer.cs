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
        // reconnectWindow: how long the seat of a user that lost its connection waits for it, as
        // SessionCreateOptions.ReconnectWindow of the backend (the others get RemoteAway, a connection with the ticket
        // of the same user returns to the seat); zero - a lost connection is a leave.
        public LocalSession Create(
            Guid id,
            SessionType type,
            int maxUsers,
            Func<SessionCore, IEnumerable<IService>> services,
            TimeSpan reconnectWindow = default)
        {
            var session = new LocalSession(_lifetime.Child(), id, type, maxUsers, services, reconnectWindow);

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
                _socket.Send(bytes).NoAwait();
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
}
