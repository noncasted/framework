using System;
using System.Collections.Generic;
using Shared;
using UnityEngine;

namespace Internal
{
    // The game server of the offline mode: sessions of one user each, the local counterpart of the backend session
    // (backend/Game/Session). The client enters a session by its ticket, as with the game gateway; the session
    // services that have backend logic are given by the creator of the session (LocalSessionService), a service of
    // another key is created empty on its first request, as the backend ServiceFactory does.
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

        public LocalSession Create(IReadOnlyList<LocalSessionService> services)
        {
            var session = new LocalSession(_lifetime.Child(), Guid.NewGuid(), services);

            _sessions.Add(session.Id, session);
            session.Lifetime.Listen(() => _sessions.Remove(session.Id));

            return session;
        }

        public string GetTicket(Guid sessionId)
        {
            if (_sessions.ContainsKey(sessionId) == false)
                throw new LocalServerRequestException(ErrorCode.NotFound, $"Session {sessionId} is not on the local server");

            return TicketPrefix + sessionId.ToString("N");
        }

        public ILocalServerConnection Accept(IReadOnlyLifetime lifetime, ILocalClient client)
        {
            return new LocalSessionConnection(lifetime, client, this);
        }

        public LocalSession FindByTicket(string ticket)
        {
            if (ticket.StartsWith(TicketPrefix, StringComparison.Ordinal) == false)
                return null;

            if (Guid.TryParseExact(ticket.Substring(TicketPrefix.Length), "N", out var id) == false)
                return null;

            return _sessions.TryGetValue(id, out var session) == true ? session : null;
        }
    }

    public class LocalSession
    {
        public LocalSession(ILifetime lifetime, Guid id, IReadOnlyList<LocalSessionService> services)
        {
            _lifetime = lifetime;
            Id = id;

            foreach (var service in services)
                AddService(service);
        }

        // The only user of a local session: the indices of the backend start from 1.
        public const int UserIndex = 1;

        private readonly ILifetime _lifetime;
        private readonly Dictionary<string, LocalSessionService> _services = new();
        private readonly Dictionary<int, LocalSessionService> _servicesById = new();
        private readonly Dictionary<int, IEntityPayload> _entities = new();
        private readonly HashSet<int> _removed = new();

        // The id of the user in this session (SharedSessionPlayer.LocalUpdate.Id).
        private readonly Guid _userId = Guid.NewGuid();

        private LocalSessionConnection _user;

        public Guid Id { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public SessionAuthStatus GetEntryStatus()
        {
            if (_lifetime.IsTerminated == true)
                return SessionAuthStatus.Closed;

            // A local session is entered once: its user leaving ends it.
            if (_user != null)
                return SessionAuthStatus.NoReservation;

            return SessionAuthStatus.Ok;
        }

        public void Join(LocalSessionConnection user)
        {
            if (GetEntryStatus() != SessionAuthStatus.Ok)
                throw new InvalidOperationException($"[Network] [Local] Session {Id} can't be entered: {GetEntryStatus()}");

            _user = user;
            user.Lifetime.Listen(_lifetime.Terminate);

            Debug.Log($"[Network] [Local] User {UserIndex} joins session {Id}");

            user.Send(new SharedSessionPlayer.LocalUpdate { Index = UserIndex, Id = _userId });

            foreach (var (_, service) in _services)
                service.OnOwnerChanged(UserIndex);
        }

        public void SendAll(INetworkContext context)
        {
            _user?.Send(context);
        }

        public int CreateEntity(int id, IEntityPayload payload)
        {
            // As the backend EntityCreateCommand: an id out of the range of the creator or used before is refused.
            if (SharedEntityIds.IsInRangeOf(id, UserIndex) == false)
                throw new LocalServerRequestException(ErrorCode.Forbidden, $"Entity {id} is out of the id range of user {UserIndex}");

            if (_entities.ContainsKey(id) == true || _removed.Contains(id) == true)
                throw new LocalServerRequestException(ErrorCode.Forbidden, $"Entity id {id} is already used in the session");

            _entities.Add(id, payload);
            return id;
        }

        public void DestroyEntity(int id)
        {
            if (_entities.Remove(id) == true)
            {
                _removed.Add(id);
                return;
            }

            // A destroy that crossed the destroy of the session (a travel): dropped, as on the backend.
            if (_removed.Contains(id) == true)
                return;

            throw new InvalidOperationException($"[Network] [Local] Entity {id} is destroyed but never existed in the session");
        }

        // The session entities of a scene of the session that is left (the game services of the backend destroy them
        // the same way): owned by the session and not persistent. The user gets DestroyUpdate for each.
        public int DestroySessionEntities()
        {
            var destroyed = new List<int>();

            foreach (var (id, payload) in _entities)
            {
                if (payload is ISessionEntityPayload && payload is not IPersistentSessionEntityPayload)
                    destroyed.Add(id);
            }

            foreach (var id in destroyed)
            {
                _entities.Remove(id);
                _removed.Add(id);
                SendAll(new SharedSessionEntity.DestroyUpdate { EntityId = id });
            }

            return destroyed.Count;
        }

        public void WriteProperties(IReadOnlyList<SharedSessionObject.SetProperty> entries)
        {
            foreach (var entry in entries)
            {
                // A failed entry is reported, the others apply (backend SetPropertiesCommand).
                try
                {
                    WriteProperty(entry);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public void WriteProperty(SharedSessionObject.SetProperty entry)
        {
            if (_servicesById.TryGetValue(entry.ObjectId, out var service) == true)
            {
                service.Write(_user, entry.PropertyId, entry.Value);
                return;
            }

            // Entity properties go only to the other users: the local session has none.
            CheckObject(entry.ObjectId);
        }

        public void OnEvent(SharedSessionObject.Event context)
        {
            // Forwarded only to the other users: the local session has none.
            if (_servicesById.ContainsKey(context.ObjectId) == false)
                CheckObject(context.ObjectId);
        }

        public LocalSessionService GetOrCreateService(SharedSessionService.GetRequest request)
        {
            if (_services.TryGetValue(request.Key, out var service) == false)
            {
                service = new LocalSessionService(request.Key);

                foreach (var propertyId in request.PropertiesIds)
                    service.BindProperty(new LocalServiceProperty(propertyId, Array.Empty<byte>()));

                AddService(service);
            }

            service.AddSubscriber(_user);
            return service;
        }

        private void AddService(LocalSessionService service)
        {
            _services.Add(service.Key, service);
            _servicesById.Add(service.Id, service);
            service.Start(this, _lifetime);
        }

        private void CheckObject(int objectId)
        {
            if (_entities.ContainsKey(objectId) == true || _removed.Contains(objectId) == true)
                return;

            throw new InvalidOperationException($"[Network] [Local] Object {objectId} does not exist in the session");
        }
    }

    public class LocalSessionConnection : LocalServerConnection
    {
        public LocalSessionConnection(IReadOnlyLifetime lifetime, ILocalClient client, LocalSessionServer server)
            : base(lifetime, client)
        {
            _server = server;
        }

        private readonly LocalSessionServer _server;

        private LocalSession _session;

        protected override void OnRequest(int requestId, INetworkContext context)
        {
            if (context is SharedSessionAuth.Request auth)
            {
                Authorize(requestId, auth);
                return;
            }

            var session = GetSession();

            switch (context)
            {
                case SharedSessionEntity.CreateRequest create:
                    var id = session.CreateEntity(create.Id, create.Payload);
                    Respond(requestId, new SharedSessionEntity.CreateResponse { EntityId = id });
                    break;

                case SharedSessionService.GetRequest get:
                {
                    var service = session.GetOrCreateService(get);

                    Respond(requestId, new SharedSessionService.GetResponse
                    {
                        Key = service.Key,
                        Id = service.Id,
                        Properties = service.CreateOverview()
                    });

                    break;
                }

                default:
                    throw new LocalServerRequestException(ErrorCode.UnknownCommand, $"No command for {context.GetType().Name}");
            }
        }

        protected override void OnOneWay(INetworkContext context)
        {
            var session = GetSession();

            switch (context)
            {
                case SharedSessionObject.SetProperties properties:
                    session.WriteProperties(properties.Entries);
                    break;

                case SharedSessionObject.SetProperty property:
                    session.WriteProperty(property);
                    break;

                case SharedSessionObject.Event objectEvent:
                    session.OnEvent(objectEvent);
                    break;

                case SharedSessionEntity.Destroy destroy:
                    session.DestroyEntity(destroy.EntityId);
                    break;

                case SharedSessionLatency.Ping ping:
                    Send(new SharedSessionLatency.Pong { Id = ping.Id });
                    break;

                // Peers of the latency overlay: a local session has no other users.
                case SharedSessionLatency.PeerProbe:
                case SharedSessionLatency.PeerEcho:
                    break;

                // The end of the connection ends the session either way.
                case SharedSessionPlayer.Leave:
                    break;

                default:
                    throw new InvalidOperationException($"[Network] [Local] No command for {context.GetType().Name}");
            }
        }

        private void Authorize(int requestId, SharedSessionAuth.Request auth)
        {
            if (_session != null)
                throw new LocalServerRequestException(ErrorCode.InvalidRequest, "The connection is already authorized");

            var session = _server.FindByTicket(auth.Ticket);
            var status = session == null ? SessionAuthStatus.InvalidTicket : session.GetEntryStatus();

            // The response goes before LocalUpdate, as from the game gateway.
            Respond(requestId, new SharedSessionAuth.Response { Status = status });

            if (status != SessionAuthStatus.Ok)
            {
                Close(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"session entry refused: {status}"));
                return;
            }

            _session = session;
            session.Join(this);
        }

        private LocalSession GetSession()
        {
            if (_session == null)
                throw new InvalidOperationException("[Network] [Local] A message before the session authorization");

            return _session;
        }
    }
}
