using System;
using Game.Session;
using Shared;

namespace Internal
{
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
}
