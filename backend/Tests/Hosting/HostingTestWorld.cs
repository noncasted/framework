using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Lobbies;
using Game.Session;
using Internal;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;

namespace Framework.Tests.Hosting;

// One game server of the hosting module: the real SessionsCollection, LobbySessions and LobbyCodeLeases over an
// in-memory code registry, with real session containers (TestSession: AddSessionServices + the lobby services) on
// manual queues and a shared manual clock. The world plays the game: it builds the sessions (ILobbyFactory,
// ILobbyMatches, IKnownUsersMatches), searches them (ISessionSearch) and decides when a running match lets a
// late player in (TestLobbyEntryPolicy).
public class HostingTestWorld : ILobbyFactory, ILobbyMatches, IKnownUsersMatches, ISessionSearch
{
    public HostingTestWorld(int maxUsers = 4)
    {
        Options = new LobbyOptions
        {
            MaxUsers = maxUsers
        };

        Self = new GameServerOverview
        {
            Id = ServerId,
            Tag = TestRoles.Game,
            UpdateTime = Time.GetUtcNow().UtcDateTime,
            Url = ServerUrl
        };

        Servers.Add(ServerId, Self);

        Discovery = Substitute.For<IServiceDiscovery>();
        Discovery.Self.Returns(Self);
        Discovery.Entries.Returns(Servers);

        Collection = new SessionsCollection(
            Substitute.For<IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>>>(),
            Time);

        Leases = new LobbyCodeLeases(Codes, NullLogger<LobbyCodeLeases>.Instance);

        Lobbies = new LobbySessions(
            this,
            Collection,
            Codes,
            Leases,
            EntryPolicy,
            Options,
            Discovery,
            NullLogger<LobbySessions>.Instance);
    }

    public const string ServerUrl = "http://localhost:7102";

    // The reservation time of every session: the default of the framework options.
    public static readonly TimeSpan JoinTimeout = new SessionCreateOptions
    {
        Type = SessionType.Match,
        ExpectedUsers = 0,
        MaxUsers = 1,
        IsPrivate = false
    }.JoinTimeout;

    public readonly ManualTimeProvider Time = new();
    public readonly Guid ServerId = Guid.NewGuid();
    public readonly GameServerOverview Self;

    // What the service discovery knows (the meta reads it): this server, until a test removes it.
    public readonly Dictionary<Guid, IServiceOverview> Servers = new();
    public readonly IServiceDiscovery Discovery;
    public readonly LobbyOptions Options;
    public readonly SessionsCollection Collection;
    public readonly FakeLobbyCodes Codes = new();
    public readonly LobbyCodeLeases Leases;
    public readonly TestLobbyEntryPolicy EntryPolicy = new();
    public readonly LobbySessions Lobbies;
    public readonly Dictionary<Guid, TestSession> Sessions = new();

    // Not null: the lobby factory of the game fails with it.
    private Exception? _lobbyCreationFailure;

    public string WebSocketUrl => ServerUrl.ServerUrlToWebSocket();

    // The place check of the gateway path (after the ticket): null is the refusal.
    public IUser? Enter(Guid sessionId, Guid userId)
    {
        if (Collection.TryGet(sessionId, out _) == false)
            return null;

        return Sessions[sessionId].Enter(userId);
    }

    // The user leaves (SharedSessionPlayer.Leave, then the socket closes): its seat ends in the session queue.
    public void Disconnect(TestSession session, IUser user)
    {
        session.Leave(user);
    }

    // The socket is lost without a leave.
    public void LoseConnection(TestSession session, IUser user)
    {
        session.Lose(user);
    }

    public void DrainAll()
    {
        List<TestSession> sessions;

        lock (Sessions)
            sessions = Sessions.Values.ToList();

        foreach (var session in sessions)
            session.Queue.Drain();
    }

    // A public match of the search (the game without the menu).
    public TestSession CreatePublicMatch()
    {
        return Add(MatchOptions(isPrivate: false, maxUsers: Options.MaxUsers), _ => { });
    }

    public void FailLobbyCreation(Exception failure)
    {
        _lobbyCreationFailure = failure;
    }

    // ILobbyFactory: a lobby session with the lobby service of the framework.
    ISession ILobbyFactory.Create(LobbyCreation creation)
    {
        if (_lobbyCreationFailure != null)
            throw _lobbyCreationFailure;

        var options = new SessionCreateOptions
        {
            Id = creation.Id,
            Type = SessionType.Lobby,
            ExpectedUsers = 0,
            MaxUsers = creation.MaxUsers,
            IsPrivate = true,
            Time = Time
        };

        var session = Add(options, services => {
            services.AddSingleton<ILobbyMatches>(this);
            services.AddSingleton<ILobbyListings>(Leases);

            services.AddLobbyService(new LobbySessionInfo
            {
                SessionId = creation.Id,
                Code = creation.Code,
                LobbySessionId = creation.LobbySessionId,
                RunningMatchId = creation.RunningMatchId,
                MaxUsers = creation.MaxUsers
            });
        });

        return session.Session;
    }

    // ILobbyMatches: what the game does at zero of the countdown - a private match with a place for every user of the
    // lobby, listed by the code of the lobby; a closed room of one player is a match of one place.
    public LobbyMatch Create(Guid lobbySessionId, IReadOnlyList<Guid> userIds, bool isOpen)
    {
        if (userIds.Count > Options.MaxUsers)
            throw new InvalidOperationException($"A lobby starts a match for {userIds.Count} users, the limit is {Options.MaxUsers}");

        var maxUsers = isOpen == false && userIds.Count == 1 ? 1 : Options.MaxUsers;

        var session = Add(MatchOptions(isPrivate: true, maxUsers), services => {
            services.AddSingleton<ILobbyListings>(Leases);

            services.AddMatchListingService(new MatchListingInfo
            {
                LobbySessionId = lobbySessionId,
                IsOpen = isOpen,
                MaxUsers = Options.MaxUsers
            });
        });

        foreach (var userId in userIds)
        {
            if (session.Session.TryReserve(userId) == false)
                throw new InvalidOperationException($"Created match session {session.Session.Id} rejected the reservation");
        }

        Leases.PassToMatch(lobbySessionId, session.Session);

        return new LobbyMatch
        {
            ServerUrl = WebSocketUrl,
            SessionId = session.Session.Id
        };
    }

    public LobbyMatch? Admit(Guid matchSessionId, Guid userId)
    {
        if (Collection.TryGet(matchSessionId, out var session) == false || session.Type != SessionType.Match)
            return null;

        if (session.TryReserve(userId) == false)
            return null;

        return new LobbyMatch
        {
            ServerUrl = WebSocketUrl,
            SessionId = matchSessionId
        };
    }

    // IKnownUsersMatches: a private match for exactly these users, their places reserved at the creation.
    ISession IKnownUsersMatches.Create(IReadOnlyList<Guid> userIds)
    {
        if (userIds.Count == 0 || userIds.Distinct().Count() != userIds.Count)
            throw new ArgumentException($"A match for known users needs distinct users, got {userIds.Count}", nameof(userIds));

        var session = Add(new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = userIds.Count,
            MaxUsers = userIds.Count,
            IsPrivate = true,
            Time = Time
        }, _ => { });

        foreach (var userId in userIds)
        {
            if (session.Session.TryReserve(userId) == false)
                throw new InvalidOperationException($"Created match session {session.Session.Id} rejected the reservation of {userId}");
        }

        return session.Session;
    }

    // ISessionSearch: a public match with a free place, otherwise a new one. Nothing of the game: no late entry.
    public Guid GetOrCreate(Guid userId, out bool isLobby)
    {
        isLobby = false;

        foreach (var (_, session) in Collection.Entries)
        {
            if (session.Type != SessionType.Match || session.IsPrivate == true)
                continue;

            if (session.TryReserve(userId) == true)
                return session.Id;
        }

        var created = CreatePublicMatch();

        if (created.Session.TryReserve(userId) == false)
            throw new InvalidOperationException($"Created session {created.Session.Id} rejected the reservation");

        return created.Session.Id;
    }

    private SessionCreateOptions MatchOptions(bool isPrivate, int maxUsers)
    {
        return new SessionCreateOptions
        {
            Type = SessionType.Match,
            ExpectedUsers = 0,
            MaxUsers = maxUsers,
            IsPrivate = isPrivate,
            Time = Time
        };
    }

    // Same order as the factory of a game: the session is in the collection before its services start and it runs.
    private TestSession Add(SessionCreateOptions options, Action<IServiceCollection> configure)
    {
        var session = new TestSession(options, configure);

        lock (Sessions)
            Sessions.Add(session.Session.Id, session);

        Collection.Add(session.Session);
        var created = session.Queue.SetupServices(session.ServiceFactory);
        session.Queue.Drain();
        created.GetAwaiter().GetResult();
        session.Run();

        return session;
    }
}

// When a running match lets a late player in: the tests switch it.
public class TestLobbyEntryPolicy : ILobbyEntryPolicy
{
    // The matches that have ended (NotFound for their late players).
    public readonly HashSet<Guid> OverMatches = new();

    // False: no running match can be entered now (Started).
    private bool _canEnter = true;

    public void SetCanEnter(bool canEnter)
    {
        _canEnter = canEnter;
    }

    public bool IsMatchOver(Guid matchSessionId)
    {
        lock (OverMatches)
            return OverMatches.Contains(matchSessionId);
    }

    public bool CanEnterMatchNow(Guid matchSessionId)
    {
        return _canEnter;
    }
}

public static class HostingTestSessionExtensions
{
    // The lobby service of a lobby session.
    public static LobbyService LobbyService(this TestSession session)
    {
        return session.Provider.GetServices<IService>().OfType<LobbyService>().Single();
    }

    // The listing service of a match started by a lobby; null for any other session.
    public static MatchListingService? MatchListing(this TestSession session)
    {
        return session.Provider.GetServices<IService>().OfType<MatchListingService>().SingleOrDefault();
    }

    public static LobbySessionInfo LobbyInfo(this TestSession session)
    {
        return session.Provider.GetRequiredService<LobbySessionInfo>();
    }

    // A join by the code of a lobby that has not started its match (what the meta sends then).
    public static LobbyJoinStatus JoinLobby(this LobbySessions lobbies, Guid sessionId, Guid userId)
    {
        return lobbies.Join(sessionId, Guid.Empty, string.Empty, userId).Status;
    }

    public static T Read<T>(this IService service, int propertyId)
    {
        return MemoryPackSerializer.Deserialize<T>(service.Properties[propertyId].RawValue)!;
    }
}

// The registry grain without the cluster: same rules (LobbyRegistry), one dictionary.
public class FakeLobbyCodes : ILobbyCodes
{
    public readonly Dictionary<string, LobbyRegistryEntry> Entries = new();

    // Null: real random codes.
    private Func<string>? _generator;

    // Not null: the registry answers MarkStarted only when the gate is completed (a slow grain call).
    public TaskCompletionSource? MarkStartedGate { get; private set; }

    // Every registry call in the order the registry got them: "Mark CODE", "Listing CODE", "Release CODE".
    public readonly List<string> Calls = new();

    public void SetGenerator(Func<string> generator)
    {
        _generator = generator;
    }

    public void HoldMarkStarted(TaskCompletionSource gate)
    {
        MarkStartedGate = gate;
    }

    public Task UpdateListing(string code, Guid sessionId, LobbyListing listing)
    {
        lock (Entries)
        {
            if (Entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
                throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

            entry.UpdateListing(listing);
            Calls.Add($"Listing {code}");
            return Task.CompletedTask;
        }
    }

    public Task<OpenLobbies> GetOpenLobbies()
    {
        lock (Entries)
        {
            var lobbies = new OpenLobbies();

            foreach (var (code, entry) in Entries)
            {
                if (entry.Listing.IsListed == true)
                {
                    lobbies.Entries.Add(new OpenLobby
                    {
                        Code = code,
                        ServerId = entry.ServerId,
                        Listing = entry.Listing
                    });
                }
            }

            return Task.FromResult(lobbies);
        }
    }

    // The open lobby of the code, null if it is not listed.
    public LobbyListing? FindListed(string code)
    {
        lock (Entries)
        {
            if (Entries.TryGetValue(code, out var entry) == false || entry.Listing.IsListed == false)
                return null;

            return entry.Listing;
        }
    }

    public Task<string> Register(Guid serverId, Guid sessionId)
    {
        lock (Entries)
        {
            var code = LobbyCodeGenerator.GenerateUnique(Entries.ContainsKey, _generator ?? LobbyCodeGenerator.Generate);

            Entries.Add(code, new LobbyRegistryEntry
            {
                ServerId = serverId,
                SessionId = sessionId
            });

            return Task.FromResult(code);
        }
    }

    public Task<LobbyRegistryEntry?> Find(string code)
    {
        lock (Entries)
            return Task.FromResult(Entries.GetValueOrDefault(code));
    }

    public async Task MarkStarted(string code, Guid sessionId, Guid matchSessionId)
    {
        if (MarkStartedGate != null)
            await MarkStartedGate.Task;

        lock (Entries)
        {
            if (Entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
                throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

            if (entry.MatchSessionId != Guid.Empty)
                throw new InvalidOperationException($"Lobby code {code} is already marked as started");

            entry.MarkStarted(matchSessionId);
            Calls.Add($"Mark {code}");
        }
    }

    public Task Release(string code, Guid sessionId)
    {
        lock (Entries)
        {
            if (Entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
                throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

            Entries.Remove(code);
            Calls.Add($"Release {code}");
            return Task.CompletedTask;
        }
    }
}

// A lobby session of HostingTestWorld with its users connected: the start of the lobby is driven the way the client
// of the leader and the clock of the server do it.
public class TestLobby
{
    private TestLobby(HostingTestWorld world, TestSession session, IReadOnlyList<IUser> users)
    {
        World = world;
        Session = session;
        Users = users;
    }

    public readonly HostingTestWorld World;
    public readonly TestSession Session;

    // The creator (the leader) first, then the guests in the order they entered.
    public readonly IReadOnlyList<IUser> Users;

    public LobbyService Service => Session.LobbyService();
    public LobbyState State => Service.Read<LobbyState>(SharedLobbyService.LobbyPropertyId);
    public LobbyStartState Start => Service.Read<LobbyStartState>(SharedLobbyService.StartPropertyId);
    public LobbyCountdownState Countdown => Service.Read<LobbyCountdownState>(SharedLobbyService.CountdownPropertyId);
    public LobbyGameState Game => Service.Read<LobbyGameState>(SharedLobbyService.GamePropertyId);
    public string Code => State.Code;

    public static Task<TestLobby> Create(int guests)
    {
        return Create(new HostingTestWorld(), guests);
    }

    public static async Task<TestLobby> Create(HostingTestWorld world, int guests)
    {
        var creator = Guid.NewGuid();

        var created = await world.Lobbies.Create(creator);
        var users = new List<IUser> { world.Enter(created.SessionId, creator)! };

        for (var i = 0; i < guests; i++)
        {
            var guest = Guid.NewGuid();

            world.Lobbies.Join(created.SessionId, Guid.Empty, created.Code, guest).Status.Should().Be(LobbyJoinStatus.Ok);
            users.Add(world.Enter(created.SessionId, guest)!);
        }

        return new TestLobby(world, world.Sessions[created.SessionId], users);
    }

    // A write of the leader's client: the real SetPropertyCommand in the session queue.
    public void WriteStart(bool isStarting)
    {
        WriteStart(isStarting, Session.Owner.Owner.Value!);
    }

    public void WriteVisibility(bool isOpen)
    {
        WriteVisibility(isOpen, Session.Owner.Owner.Value!);
    }

    public void WriteVisibility(bool isOpen, IUser writer)
    {
        Write(writer, SharedLobbyService.VisibilityPropertyId, new LobbyVisibilityState
        {
            IsOpen = isOpen
        });
    }

    public void WriteStart(bool isStarting, IUser writer)
    {
        Write(writer, SharedLobbyService.StartPropertyId, new LobbyStartState
        {
            IsStarting = isStarting
        });
    }

    public void Write<T>(IUser writer, int propertyId, T value)
    {
        var context = new SharedSessionObject.SetProperty
        {
            ObjectId = Service.Id,
            PropertyId = propertyId,
            Value = MemoryPackSerializer.Serialize(value)
        };

        Session.Queue.Enqueue(() => Session.Execute(writer, context));
        Session.Queue.Drain();
    }

    // In steps of 100 ms: a countdown step schedules the next one when it runs.
    public void Advance(TimeSpan time)
    {
        var step = TimeSpan.FromMilliseconds(100);

        for (var passed = TimeSpan.Zero; passed < time; passed += step)
        {
            World.Time.Advance(step);
            Session.Queue.Drain();
        }
    }

    public void RunCountdown()
    {
        WriteStart(true);
        Advance(LobbyService.CountdownStep * LobbyCountdownState.Seconds);

        Game.SessionId.Should().NotBe(Guid.Empty);
    }
}
