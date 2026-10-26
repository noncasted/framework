using FluentAssertions;
using Game.Session;
using Internal;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// SessionCore: the session of a host client (P2P match, offline) without a container, on the same parts and commands
// as the backend session. Users are introduced to each other, entities and property writes go to the others, a write
// of a restricted property by another user is dropped, a leave ends the seat for the others.
public class SessionCoreTests
{
    [Fact]
    public void Connect_UsersIntroducedToEachOther()
    {
        var core = Create();
        var first = new RecordingConnection();
        var second = new RecordingConnection();

        var firstUser = core.Connect(core.Lifetime, Guid.NewGuid(), first);
        var secondUser = core.Connect(core.Lifetime, Guid.NewGuid(), second);

        firstUser.Index.Should().Be(1);
        secondUser.Index.Should().Be(2);

        first.OneWay.OfType<SharedSessionPlayer.LocalUpdate>().Single().Index.Should().Be(1);
        first.OneWay.OfType<SharedSessionPlayer.RemoteUpdate>().Single().Index.Should().Be(2);
        second.OneWay.OfType<SharedSessionPlayer.LocalUpdate>().Single().Index.Should().Be(2);
        second.OneWay.OfType<SharedSessionPlayer.RemoteUpdate>().Single().Index.Should().Be(1);

        core.Owner.Owner.Value.Should().Be(firstUser);
    }

    [Fact]
    public void EntityCreate_GoesToOthers_PropertyWriteRelayedWithVersion()
    {
        var core = Create();
        var first = new RecordingConnection();
        var second = new RecordingConnection();
        var firstUser = core.Connect(core.Lifetime, Guid.NewGuid(), first);
        core.Connect(core.Lifetime, Guid.NewGuid(), second);

        var id = SharedEntityIds.Create(firstUser.Index, 1);

        core.OnRequest(firstUser, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Payload = new TestPayload(),
            Properties = new[] { new SharedSessionObject.PropertyUpdate { PropertyId = 5, Value = new byte[] { 1 } } }
        }, 10);

        first.Responses.Should().ContainSingle(r => r.RequestId == 10 && r.Context is SharedSessionEntity.CreateResponse);
        second.OneWay.OfType<SharedSessionEntity.CreatedOverview>().Single().EntityId.Should().Be(id);

        core.OnOneWay(firstUser, new SharedSessionObject.SetProperty { ObjectId = id, PropertyId = 5, Value = new byte[] { 2 } });

        var update = second.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Single();
        update.Value.Should().Equal(2);
        update.Version.Should().Be(1);
        first.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Should().BeEmpty();
    }

    [Fact]
    public void ServiceProperty_RestrictedWriter_OtherUserWriteDropped()
    {
        var core = Create(start: false);
        var service = new Service("Game");
        // A plain type of the protocol stands for the state of a game service.
        var property = new ValueProperty<SharedSessionLatency.Ping>(1);
        property.RestrictWriter(user => user.Index == 1);
        service.BindProperty(property);
        core.AddService(service);
        core.Start();

        var first = new RecordingConnection();
        var second = new RecordingConnection();
        var firstUser = core.Connect(core.Lifetime, Guid.NewGuid(), first);
        var secondUser = core.Connect(core.Lifetime, Guid.NewGuid(), second);

        var request = new SharedSessionService.GetRequest { Key = "Game", PropertiesIds = new[] { 1 } };
        core.OnRequest(firstUser, request, 1);
        core.OnRequest(secondUser, request, 1);

        var value = MemoryPack.MemoryPackSerializer.Serialize(new SharedSessionLatency.Ping { Id = 7 });

        core.OnOneWay(secondUser, new SharedSessionObject.SetProperty { ObjectId = service.Id, PropertyId = 1, Value = value });
        property.Value.Id.Should().Be(0);
        first.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Should().BeEmpty();

        core.OnOneWay(firstUser, new SharedSessionObject.SetProperty { ObjectId = service.Id, PropertyId = 1, Value = value });
        property.Value.Id.Should().Be(7);
        second.OneWay.OfType<SharedSessionObject.PropertyUpdate>().Single().Version.Should().Be(1);
    }

    [Fact]
    public void Leave_SeatEnded_OthersToldAndEntitiesDestroyed_OwnerPasses()
    {
        var core = Create();
        var first = new RecordingConnection();
        var second = new RecordingConnection();
        var firstLifetime = core.Lifetime.Child();
        var firstUser = core.Connect(firstLifetime, Guid.NewGuid(), first);
        var secondUser = core.Connect(core.Lifetime, Guid.NewGuid(), second);

        var id = SharedEntityIds.Create(firstUser.Index, 1);
        core.OnRequest(firstUser, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Payload = new TestPayload(),
            Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
        }, 1);

        firstLifetime.Terminate();

        second.OneWay.OfType<SharedSessionPlayer.RemoteDisconnect>().Single().Index.Should().Be(1);
        core.Entities.Entries.Should().NotContainKey(id);
        core.Users.Should().Equal(secondUser);
        core.Owner.Owner.Value.Should().Be(secondUser);
    }

    [Fact]
    public void Ping_Answered()
    {
        var core = Create();
        var connection = new RecordingConnection();
        var user = core.Connect(core.Lifetime, Guid.NewGuid(), connection);

        core.OnOneWay(user, new SharedSessionLatency.Ping { Id = 3 });

        connection.OneWay.OfType<SharedSessionLatency.Pong>().Single().Id.Should().Be(3);
    }

    private static SessionCore Create(bool start = true)
    {
        var core = new SessionCore(Guid.NewGuid(), SessionType.Match, new Lifetime(), new NullSessionLog());

        if (start == true)
            core.Start();

        return core;
    }

    private class RecordingConnection : IUserConnection
    {
        public readonly List<INetworkContext> OneWay = new();
        public readonly List<(INetworkContext Context, int RequestId)> Responses = new();

        public void Send(INetworkContext context)
        {
            OneWay.Add(context);
        }

        public void Respond(INetworkContext context, int requestId)
        {
            Responses.Add((context, requestId));
        }

        public void Disconnect()
        {
        }
    }
}

