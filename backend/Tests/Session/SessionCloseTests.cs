using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// A session never outlives its users: it closes when the last human leaves (bots don't hold it), when nobody enters
// it, when its expected users don't all join in time. Real sessions on manual time and queues: see TestSession.
public class SessionCloseTests
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void LastUserLeft_TerminatesSession()
    {
        var session = TestSession.Create();
        var user = session.ReserveAndEnter();

        session.Leave(user);

        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void LastUserLeft_SessionIsUnavailableBeforeItsLifetimeIsTerminated()
    {
        var session = TestSession.Create();
        var user = session.ReserveAndEnter();
        var connection = TestSession.Connection(user);

        // The user is removed, the termination of the session is still waiting in its queue.
        connection.ReceiveOneWay(new SharedSessionPlayer.Leave());
        connection.Close();

        while (user.Lifetime.IsTerminated == false)
            session.Queue.RunNext().Should().BeTrue();

        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeFalse();

        var other = Guid.NewGuid();
        session.Reserve(other).Should().BeFalse();
        session.Session.HasFreePlace(other).Should().BeFalse();
        session.Enter(other).Should().BeNull();

        session.Queue.Drain();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void OwnerLeft_SecondUserStays_OwnerPasses_SessionAlive()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        session.Owner.Owner.Value.Should().Be(first);

        session.Leave(first);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().Be(second);
        session.Users.Should().Equal(second);

        session.Leave(second);

        session.Owner.Owner.Value.Should().BeNull();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void SecondUserLeft_FirstStays_LeftUserReturnsWithReservation()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();
        var second = session.ReserveAndEnter(secondId);

        session.Leave(second);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().Be(first);
        session.Users.Should().Equal(first);

        // No reconnect window: a new place needs a new reservation.
        session.Enter(secondId).Should().BeNull();

        var returned = session.ReserveAndEnter(secondId);

        returned.Index.Should().NotBe(second.Index, "a new seat takes a new index");
        session.Users.Should().HaveCount(2);
    }

    // The client was lost without a close (a domain reload, a crash), its socket is still open. The next reservation
    // of the same user drops that connection: the session left with nobody is closed and refuses the reservation.
    [Fact]
    public void StaleConnectionAlone_ReserveAgain_IsRefused_DropsIt_ClosesSession()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        var stale = session.ReserveAndEnter(userId);

        session.Reserve(userId).Should().BeFalse();
        session.Session.IsClosed.Should().BeFalse("the stale connection is dropped from the queue of the session");

        session.Queue.Drain();

        TestSession.Connection(stale).ForceDisconnects.Should().Be(1);
        session.Users.Should().BeEmpty();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // Two users: the lost client of the second one comes back while the first plays on - it enters the live session
    // again, its stale connection is dropped and only the first one holds the session meanwhile.
    [Fact]
    public void StaleConnectionBesideLiveUser_ReserveAgain_RejoinsLiveSession()
    {
        var session = TestSession.Create(maxUsers: 2);
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();
        var stale = session.ReserveAndEnter(secondId);

        // Full with the stale connection, but it is not counted for its own user.
        session.Reserve(secondId).Should().BeTrue();
        session.Queue.Drain();

        TestSession.Connection(stale).ForceDisconnects.Should().Be(1);
        session.Users.Should().Equal(first);
        session.Lifetime.IsTerminated.Should().BeFalse();

        var second = session.Enter(secondId)!;

        session.Users.Should().Equal(first, second);
        session.Owner.Owner.Value.Should().Be(first);
    }

    // A third user is refused by a session of two live users: only the own stale connections are not counted.
    [Fact]
    public void FullSessionOfLiveUsers_ThirdUserIsRefused_NobodyDropped()
    {
        var session = TestSession.Create(maxUsers: 2);
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        session.Reserve(Guid.NewGuid()).Should().BeFalse();
        session.Queue.Drain();

        TestSession.Connection(first).ForceDisconnects.Should().Be(0);
        TestSession.Connection(second).ForceDisconnects.Should().Be(0);
        session.Users.Should().Equal(first, second);
    }

    [Fact]
    public void LiveReservation_HoldsEmptySession_IncomingUserBecomesOwner()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();

        session.Reserve(secondId).Should().BeTrue();
        session.Leave(first);

        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().BeNull();

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(1));

        var second = session.Enter(secondId);

        second.Should().NotBeNull();
        session.Lifetime.IsTerminated.Should().BeFalse();
        session.Owner.Owner.Value.Should().Be(second);

        // The reservation time passes: the joined user keeps the session.
        session.Advance(JoinTimeout);

        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void ExpiredReservation_DoesNotHoldEmptySession_LateEnterIsRefused()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var secondId = Guid.NewGuid();

        session.Advance(TimeSpan.FromSeconds(100));

        session.Reserve(secondId).Should().BeTrue();
        session.Leave(first);

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));

        session.Lifetime.IsTerminated.Should().BeTrue();
        session.Enter(secondId).Should().BeNull();
    }

    [Fact]
    public void ReservedButNeverEntered_SessionClosesAfterJoinTimeout()
    {
        var session = TestSession.Create();
        session.Reserve(Guid.NewGuid()).Should().BeTrue();

        session.Advance(JoinTimeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // Nobody reserves or enters: closed after JoinTimeout of the options.
    [Fact]
    public void NobodyEver_SessionClosesAfterJoinTimeoutOfOptions()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var session = TestSession.Create(joinTimeout: timeout);

        session.Advance(timeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void RepeatedReserve_ExtendsReservation()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();

        session.Advance(TimeSpan.FromSeconds(20));
        session.Reserve(userId).Should().BeTrue();

        session.Advance(TimeSpan.FromSeconds(20));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(10));
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void SessionWithUser_IsNotClosedByTime()
    {
        var session = TestSession.Create();
        session.ReserveAndEnter();

        session.Advance(TimeSpan.FromHours(1));

        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public void JoinFailed_ClosesEmptySession()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();

        session.Session.TryAccept(userId).Should().BeTrue();
        session.Queue.Enqueue(() => session.Session.OnJoinFailed(userId));
        session.Queue.Drain();

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void CancelReservation_OfLastHolder_ClosesSession()
    {
        var session = TestSession.Create();
        var userId = Guid.NewGuid();
        session.Reserve(userId).Should().BeTrue();

        session.Session.CancelReservation(userId);
        session.Queue.Drain();

        session.Session.IsClosed.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void Run_DoesNotTerminateSession()
    {
        var session = TestSession.Create(run: false);
        session.Reserve(Guid.NewGuid()).Should().BeTrue();

        // Run sets the handling up in the session queue.
        session.Run();

        session.RunTask.IsCompletedSuccessfully.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // The expected users have not all joined within JoinTimeout: the session closes (its lifetime ends) and Run
    // completes without handling anybody.
    [Fact]
    public void ExpectedUserNeverJoins_ClosesAfterJoinTimeout()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var session = TestSession.Create(expectedUsers: 2, joinTimeout: timeout);
        var allConnected = false;
        session.Session.AllUsersConnected.Advise(session.Session.Lifetime, () => allConnected = true);

        var first = session.ReserveAndEnter();

        session.Advance(timeout - TimeSpan.FromSeconds(1));
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Advance(TimeSpan.FromSeconds(1));

        session.Lifetime.IsTerminated.Should().BeTrue();
        session.RunTask.IsCompleted.Should().BeTrue();
        allConnected.Should().BeFalse();
        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(first).Should().BeEmpty();
    }

    [Fact]
    public void ExpectedUsersAllJoined_JoinTimeoutDoesNotClose()
    {
        var session = TestSession.Create(expectedUsers: 2);
        session.ReserveAndEnter();
        session.ReserveAndEnter();

        session.Advance(JoinTimeout * 2);

        session.RunTask.IsCompletedSuccessfully.Should().BeTrue();
        session.Lifetime.IsTerminated.Should().BeFalse();
    }

    // A bot never holds the session: once the last human leaves (a leave or a lost connection), it closes.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HumanWithBot_HumanLeaves_SessionCloses(bool isLeave)
    {
        var session = TestSession.Create(expectedUsers: 2);
        var human = session.ReserveAndEnter();
        var bot = session.AddBot();

        bot.IsBot.Should().BeTrue();
        session.RunTask.IsCompletedSuccessfully.Should().BeTrue("the bot counts for the expected users");
        session.Lifetime.IsTerminated.Should().BeFalse();

        if (isLeave == true)
            session.Leave(human);
        else
            session.Lose(human);

        session.Lifetime.IsTerminated.Should().BeTrue();
        bot.Lifetime.IsTerminated.Should().BeTrue("the bot is connected as long as the session");
    }

    // Bots are not counted by the admission: a session of bots only is empty for it.
    [Fact]
    public void BotsOnly_NoHumanEver_ClosesAfterJoinTimeout()
    {
        var session = TestSession.Create();
        session.AddBot();
        session.AddBot();

        session.Session.HasFreePlace(Guid.NewGuid()).Should().BeTrue();

        session.Advance(JoinTimeout);

        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    [Fact]
    public void BotsTakeNoPlace()
    {
        var session = TestSession.Create(maxUsers: 1);
        session.AddBot();
        session.AddBot();

        session.ReserveAndEnter();

        session.Users.Should().HaveCount(3);
        session.Reserve(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void OneOfTwoHumansLeaves_WithBot_SessionStaysOpen_UntilTheOtherLeaves()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();
        session.AddBot();

        session.Lose(first);
        session.Lifetime.IsTerminated.Should().BeFalse();

        session.Leave(second);
        session.Lifetime.IsTerminated.Should().BeTrue();
    }

    // The reservation and the gateway on their own thread against the session queue that removes the last user.
    // Either the user enters a live session, or it is refused; nothing throws.
    [Fact]
    public void EnterConcurrentlyWithLastUserLeaving_EntersLiveSessionOrIsRefused()
    {
        for (var i = 0; i < 300; i++)
        {
            var session = TestSession.Create();
            var first = session.ReserveAndEnter();
            var secondId = Guid.NewGuid();

            var reserved = false;
            var accepted = false;

            using var barrier = new Barrier(2);

            Parallel.Invoke(
                () => {
                    barrier.SignalAndWait();
                    session.Leave(first);
                },
                () => {
                    barrier.SignalAndWait();
                    reserved = session.Reserve(secondId);
                    accepted = session.Session.TryAccept(secondId);
                });

            accepted.Should().Be(reserved);

            if (reserved == true)
            {
                session.Lifetime.IsTerminated.Should().BeFalse("the reservation holds the session");

                var second = session.Join(secondId);
                session.Owner.Owner.Value.Should().Be(second);
            }
            else
            {
                session.Queue.Drain();
                session.Lifetime.IsTerminated.Should().BeTrue();
            }
        }
    }
}
