using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public interface ISessionConnection
    {
        UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, string ticket);
    }

    public class SessionConnection : ISessionConnection
    {
        public SessionConnection(INetworkConnection connection, ILog<SessionConnection> log)
        {
            _connection = connection;
            _log = log;
        }

        private readonly INetworkConnection _connection;
        private readonly ILog _log;

        // the first frame of the socket is the ticket of the meta; a refusal throws (the caller races the start
        // with the loss of the connection: the server closes the socket after the refusal).
        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
        {
            if (string.IsNullOrEmpty(ticket) == true)
                throw new ArgumentException("[Network] [Session] A session is entered without a ticket", nameof(ticket));

            var auth = new SharedSessionAuth.Request
            {
                Ticket = ticket
            };

            _log.Info("Connecting to the session at {ServerUrl}", serverUrl);
            await _connection.Run(lifetime, serverUrl);
            _log.Info("Connection started, authenticating...");

            var response = await _connection.Request<SharedSessionAuth.Response>(auth);

            _log.Info("Authentication response: {Status}", response.Status);

            if (response.Status != SessionAuthStatus.Ok)
                throw new SessionEntryRefusedException(response.Status);
        }
    }

    // The game server has refused the ticket (SharedSessionAuth.Response.Status). For a return into a match
    // NoReservation / Closed mean the seat is gone; otherwise it is an error of the entry.
    public class SessionEntryRefusedException : Exception
    {
        public SessionEntryRefusedException(SessionAuthStatus status)
            : base($"[Network] [Session] The session refused the entry: {status}")
        {
            Status = status;
        }

        public SessionAuthStatus Status { get; }
    }
}
