using System;
using System.Collections.Generic;
using Internal;
using Shared;

namespace Game.Session
{
    // The session composed without a container, for a host client: the P2P match of a host and the offline mode. The
    // parts and the commands are the ones the backend registers in the container of a session (AddSessionServices in
    // backend/Session: keep the two lists the same). The host owns the transport: it connects a user with its
    // connection and hands its messages over; everything runs on one thread (the main thread of the client).
    // A host has no reconnect window: a lost connection is a leave.
    public class SessionCore
    {
        public SessionCore(Guid id, SessionType type, ILifetime lifetime, ISessionLog log)
        {
            _lifetime = lifetime;
            Log = log;

            Data = new SessionData
            {
                Id = id,
                Type = type,
                Lifetime = lifetime,
                ExpectedUsers = 0
            };

            Users = new SessionUsers();
            Seats = new SessionSeats(Users, Data);
            Owner = new SessionOwner(Users, Data);
            Objects = new SessionObjects();
            Entities = new SessionEntities();
            Services = new SessionServices();

            var propertyUpdateSender = new PropertyUpdateSender(Users);

            EntityFactory = new EntityFactory(Data, Users, Entities, Objects, propertyUpdateSender);
            ServiceFactory = new ServiceFactory(_services, Services, Data, Objects);
            Presence = new SessionPresence(Users, Seats, Entities);

            var commands = new CommandsCollection(
                new ICommand[]
                {
                    new SetPropertyCommand(Objects, Users, log),
                    new SetPropertiesCommand(Objects, Users, log),
                    new EntityDestroyCommand(Entities, Objects, Users, log),
                    new EntityEventCommand(Objects, Users, log),
                    new LeaveCommand(),
                    new PingCommand(),
                    new PeerProbeCommand(Users),
                    new PeerEchoCommand(Users),
                    new PeerSignalCommand(Users, log),
                    new PeerTunnelCommand(Users, log)
                },
                new IResponseCommand[]
                {
                    new EntityCreateCommand(EntityFactory, Objects, Users),
                    new ServiceGetOrCreateCommand(ServiceFactory)
                });

            Commands = new SessionCommandRunner(commands, log);
        }

        private readonly ILifetime _lifetime;
        private readonly List<IService> _services = new();

        private bool _isStarted;

        public ISessionData Data { get; }
        public ISessionLog Log { get; }
        public ISessionUsers Users { get; }
        public ISessionSeats Seats { get; }
        public ISessionOwner Owner { get; }
        public ISessionObjects Objects { get; }
        public ISessionEntities Entities { get; }
        public ISessionServices Services { get; }
        public IEntityFactory EntityFactory { get; }
        public IServiceFactory ServiceFactory { get; }
        public ISessionPresence Presence { get; }
        public ISessionCommandRunner Commands { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        // The game logic of the session (the services the backend registers as IService), before Start.
        public void AddService(IService service)
        {
            if (_isStarted == true)
                throw new InvalidOperationException($"[Session] Service {service.Key} is added after the start");

            _services.Add(service);
        }

        public void Start()
        {
            if (_isStarted == true)
                throw new InvalidOperationException("[Session] The session is started twice");

            _isStarted = true;
            ServiceFactory.Setup();
        }

        // A connection of a user: the seat of its backend id (the same index while the seat lives), introduced to the
        // others. The end of the connection lifetime (the host closes it, the transport is lost) is a leave.
        // index: the index of the seat given by the host (0 - the next one).
        public IUser Connect(IReadOnlyLifetime connectionLifetime, Guid backendId, IUserConnection connection, int index = 0)
        {
            if (_isStarted == false)
                throw new InvalidOperationException($"[Session] User {backendId} connects before the start");

            var seat = index == 0 ? Seats.Take(backendId) : Seats.Take(backendId, index);
            var userLifetime = connectionLifetime.Intersect(_lifetime);
            var user = new SessionUser(seat, false, userLifetime, connection);

            Seats.Attach(user);
            Users.AddUser(user);
            userLifetime.Listen(() => OnEnded(user));

            Log.Info($"[Session] User {user.Index} {backendId} connected, return: {seat.IsReturn}");

            Presence.Introduce(user);
            return user;
        }

        public void OnOneWay(IUser user, INetworkContext context)
        {
            Commands.RunOneWay(user, context);
        }

        public void OnRequest(IUser user, INetworkContext context, int requestId)
        {
            var result = Commands.RunRequest(user, context, requestId);
            user.Send(result, requestId);
        }

        private void OnEnded(IUser user)
        {
            // The end of the session ends every seat by itself.
            if (Seats.Detach(user) == false || _lifetime.IsTerminated == true)
                return;

            Log.Info($"[Session] User {user.Index} {user.BackendId} left");
            Presence.EndSeat(user.Seat);
        }
    }
}
