using Internal;
using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// A session with a reconnect window (SessionCreateOptions.ReconnectWindow): a user whose connection is lost keeps its
// seat (index, entities) for the window and returns to it; a leave or the end of the window ends the seat. Without
// a window a lost connection is a leave. Real sessions on manual time and queues: see TestSession.
public class SessionReconnectTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    [Fact]
    public void LostConnection_SeatWaits_OthersSeeAway_OwnerPasses()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Admission.IsAway(first.BackendId).Should().BeTrue();
        session.Seats.Entries[first.BackendId].Connected.Should().BeNull();
        session.Seats.Entries[first.BackendId].Lifetime.IsTerminated.Should().BeFalse();

        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
        session.Owner.Owner.Value.Should().Be(second);
    }

    [Fact]
    public void Return_WithinWindow_SameSeat_IsReturn_EntityCounter_OthersSeeUpdate()
    {
        var (session, first, second) = CreateOfTwo();
        var seat = first.Seat;

        // The client of the first user has created its entity 7 and a session entity 2 in its range.
        CreateEntity(session, first, 7);
        CreateEntity(session, first, 2, new TestSessionPayload());

        session.Lose(first);
        session.Advance(Window - TimeSpan.FromSeconds(1));

        var returned = session.Enter(first.BackendId)!;

        returned.Should().NotBeNull();
        returned.Index.Should().Be(first.Index);
        returned.Id.Should().Be(first.Id);
        returned.Seat.Should().BeSameAs(seat);
        session.Admission.IsAway(first.BackendId).Should().BeFalse();

        var local = TestSession.Sent<SharedSessionPlayer.LocalUpdate>(returned).Should().ContainSingle().Subject;
        local.Index.Should().Be(first.Index);
        local.IsReturn.Should().BeTrue();
        local.EntityCounter.Should().Be(7);
        local.SeatEntities.Should().Be(1, "the entity of the seat, not the session entity it has created");

        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(second).Select(update => update.Index).Should().Contain(first.Index);

        // The window of the first loss does not end the returned seat.
        session.Advance(TimeSpan.FromSeconds(5));

        seat.Lifetime.IsTerminated.Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
    }

    [Fact]
    public void FirstConnection_IsNotReturn()
    {
        var (_, first, _) = CreateOfTwo();

        var local = TestSession.Sent<SharedSessionPlayer.LocalUpdate>(first).Should().ContainSingle().Subject;
        local.IsReturn.Should().BeFalse();
        local.EntityCounter.Should().Be(0);
        local.SeatEntities.Should().Be(0);
    }

    [Fact]
    public void WindowExpired_SeatEnds_EntitiesDestroyed_OthersSeeDisconnect_ReturnRefused()
    {
        var (session, first, second) = CreateOfTwo();
        var seat = first.Seat;
        var entity = CreateEntity(session, first, 1);

        session.Lose(first);
        entity.Lifetime.IsTerminated.Should().BeFalse("the entities of an away seat wait for its return");

        session.Advance(Window);

        seat.Lifetime.IsTerminated.Should().BeTrue();
        entity.Lifetime.IsTerminated.Should().BeTrue();
        session.Seats.Entries.Should().NotContainKey(first.BackendId);
        session.Admission.IsAway(first.BackendId).Should().BeFalse();

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        session.Enter(first.BackendId).Should().BeNull();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void AllAway_SessionHeld_ClosedWhenTheLastWindowExpires()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Advance(TimeSpan.FromSeconds(10));
        session.Lose(second);

        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(Window - TimeSpan.FromSeconds(10));

        session.Seats.Entries.Should().ContainKey(second.BackendId).And.NotContainKey(first.BackendId);
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(10));

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void AllAway_TheFirstToReturnBecomesOwner_SameIndex()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Lose(second);
        session.Owner.Owner.Value.Should().BeNull();

        var returned = session.Enter(second.BackendId)!;

        returned.Index.Should().Be(second.Index);
        session.Owner.Owner.Value.Should().Be(returned);
    }

    [Fact]
    public void Leave_EndsSeatAtOnce_NoWindow_ReturnRefused()
    {
        var (session, first, second) = CreateOfTwo();
        var entity = CreateEntity(session, first, 1);

        session.Leave(first);

        entity.Lifetime.IsTerminated.Should().BeTrue();
        session.Admission.IsAway(first.BackendId).Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().BeEmpty();

        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        session.Enter(first.BackendId).Should().BeNull();
    }

    [Fact]
    public void Leave_ThroughReplacedConnection_Throws()
    {
        var (session, first, _) = CreateOfTwo();
        var returned = session.Enter(first.BackendId)!;

        returned.Seat.Connected.Should().Be(returned);

        var leave = () => first.Seat.MarkLeaving(first);
        leave.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReturnBeforeLossNoticed_StaleDroppedSilently_NoAwayNoDisconnect()
    {
        var (session, first, second) = CreateOfTwo();

        var returned = session.Enter(first.BackendId)!;

        TestSession.Connection(first).ForceDisconnects.Should().Be(1);
        returned.Index.Should().Be(first.Index);
        session.Users.Should().Equal(second, returned);
        session.Admission.IsAway(first.BackendId).Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().BeEmpty();
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().BeEmpty();
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(returned).Single().IsReturn.Should().BeTrue();

        session.Leave(second);
        session.Leave(returned);

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Return_IgnoresLimitAndSeal()
    {
        var (session, first, _) = CreateOfTwo(maxUsers: 2);

        session.Lose(first);

        // The place of the away seat is held: the session is full.
        session.Session.HasFreePlace(Guid.NewGuid()).Should().BeFalse();
        session.Admission.Seal();

        session.Enter(first.BackendId).Should().NotBeNull();
    }

    [Fact]
    public void ReturningUser_JoinFailed_SeatEnds()
    {
        var (session, first, second) = CreateOfTwo();

        session.Lose(first);
        session.Session.TryAccept(first.BackendId).Should().BeTrue();

        session.Queue.Enqueue(() => session.Session.OnJoinFailed(first.BackendId));
        session.Queue.Drain();

        session.Seats.Entries.Should().NotContainKey(first.BackendId);
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // ReconnectWindow = Zero: no away seats, a lost connection is a leave.
    [Fact]
    public void NoWindow_LostConnection_IsLeave()
    {
        var session = TestSession.Create(reconnectWindow: TimeSpan.Zero);
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();
        var entity = CreateEntity(session, first, 1);

        session.Lose(first);

        session.Admission.IsAway(first.BackendId).Should().BeFalse();
        session.Seats.Entries.Should().NotContainKey(first.BackendId);
        first.Seat.Lifetime.IsTerminated.Should().BeTrue();
        entity.Lifetime.IsTerminated.Should().BeTrue();

        TestSession.Sent<SharedSessionPlayer.RemoteAway>(second).Should().BeEmpty();
        TestSession.Sent<SharedSessionPlayer.RemoteDisconnect>(second).Should().ContainSingle()
                   .Which.Index.Should().Be(first.Index);

        // A connection of the same user needs a reservation (no return to a seat).
        session.Enter(first.BackendId).Should().BeNull();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void NoWindow_SecondConnectionWithoutReservation_IsRefused()
    {
        var session = TestSession.Create(reconnectWindow: TimeSpan.Zero);
        var first = session.ReserveAndEnter();

        session.Enter(first.BackendId).Should().BeNull();
        TestSession.Connection(first).ForceDisconnects.Should().Be(0);
    }

    [Fact]
    public void Seat_EntityOutOfItsRange_Throws()
    {
        var seat = new SessionSeat(Guid.NewGuid(), Guid.NewGuid(), 2, new Lifetime());

        var create = () => seat.OnEntityCreated(SharedEntityIds.Create(3, 1));
        create.Should().Throw<InvalidOperationException>();
    }

    private static (TestSession, IUser, IUser) CreateOfTwo(int maxUsers = 4)
    {
        var session = TestSession.Create(maxUsers: maxUsers, reconnectWindow: Window);
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        return (session, first, second);
    }

    // The client of the user creates an entity with the counter in its range (EntityCreateCommand).
    private static IEntity CreateEntity(TestSession session, IUser creator, int counter, IEntityPayload? payload = null)
    {
        var id = SharedEntityIds.Create(creator.Index, counter);

        session.Request(creator, new SharedSessionEntity.CreateRequest
        {
            Id = id,
            Properties = [],
            Payload = payload ?? new TestPayload()
        });

        return session.Entities.Entries[id];
    }
}
