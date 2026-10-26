using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Framework.Tests.Session;

// Session.Run: a session with ExpectedUsers starts handling its users when the last expected one joins (no poll),
// and handles a user that joins after the start as well. Real session on a manual queue and time.
public class SessionRunTests
{
    [Fact]
    public void NoExpectedUsers_UserIsHandledOnJoin()
    {
        var session = TestSession.Create(run: false);
        var run = session.Run();

        var user = session.ReserveAndEnter();

        ReceivedLocalUpdate(user).Should().BeTrue();
        run.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ExpectedUsers_NotAllJoined_NobodyIsHandled()
    {
        var session = TestSession.Create(expectedUsers: 2, run: false);
        var run = session.Run();

        var first = session.ReserveAndEnter();

        TestSession.Sent(first).Should().BeEmpty();
        session.Session.AllUsersConnected.IsCompleted.Should().BeFalse();
        run.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void ExpectedUsers_LastOneJoins_EveryoneIsHandledAtOnce()
    {
        var session = TestSession.Create(expectedUsers: 2, run: false);
        var run = session.Run();

        var first = session.ReserveAndEnter();
        var second = session.ReserveAndEnter();

        ReceivedLocalUpdate(first).Should().BeTrue();
        ReceivedLocalUpdate(second).Should().BeTrue();
        session.Session.AllUsersConnected.IsCompletedSuccessfully.Should().BeTrue();
        run.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ExpectedUsers_UserJoiningAfterStart_IsHandled_OthersLearnAboutIt()
    {
        var session = TestSession.Create(expectedUsers: 1);

        var first = session.ReserveAndEnter();
        var late = session.ReserveAndEnter();

        ReceivedLocalUpdate(first).Should().BeTrue();
        ReceivedLocalUpdate(late).Should().BeTrue();

        // The late user learns about the first one and the first one about it.
        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(late).Should().ContainSingle(update => update.Index == first.Index);
        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(first).Should().ContainSingle(update => update.Index == late.Index);
    }

    [Fact]
    public void ExpectedUsers_UserHandledOnce()
    {
        var session = TestSession.Create(expectedUsers: 1);

        var user = session.ReserveAndEnter();
        session.ReserveAndEnter();

        TestSession.Sent<SharedSessionPlayer.LocalUpdate>(user).Should().ContainSingle();
    }

    // Bots count for the expected users: a match of a human and a bot starts when both are in.
    [Fact]
    public void ExpectedUsers_BotCounts()
    {
        var session = TestSession.Create(expectedUsers: 2);

        var human = session.ReserveAndEnter();
        ReceivedLocalUpdate(human).Should().BeFalse();

        var bot = session.AddBot();

        session.RunTask.IsCompletedSuccessfully.Should().BeTrue();
        ReceivedLocalUpdate(human).Should().BeTrue();
        TestSession.Sent<SharedSessionPlayer.RemoteUpdate>(human).Should().Contain(update => update.Index == bot.Index);
    }

    [Fact]
    public void ExpectedUsers_SessionEndsBeforeJoin_RunCompletes_NobodyIsHandled()
    {
        var session = TestSession.Create(expectedUsers: 2, run: false);
        var run = session.Run();

        var first = session.ReserveAndEnter();
        session.Lifetime.Terminate();
        session.Queue.Drain();

        run.IsCompleted.Should().BeTrue();
        session.Session.AllUsersConnected.IsCanceled.Should().BeTrue();
        TestSession.Sent(first).Should().BeEmpty();
    }

    // The overview of every live entity goes to a user when it is handled.
    [Fact]
    public void HandledUser_GetsOverviewOfLiveEntities()
    {
        var session = TestSession.Create();
        var first = session.ReserveAndEnter();
        var entity = (SharedSessionEntity.CreateResponse)session.Request(first, new SharedSessionEntity.CreateRequest
        {
            Id = SharedEntityIds.Create(first.Index, 1),
            Properties = [],
            Payload = new TestPayload()
        });

        var second = session.ReserveAndEnter();

        TestSession.Sent<SharedSessionEntity.CreatedOverview>(second).Should()
                   .ContainSingle(overview => overview.EntityId == entity.EntityId && overview.OwnerId == first.Index);
    }

    private static bool ReceivedLocalUpdate(IUser user)
    {
        return TestSession.Sent<SharedSessionPlayer.LocalUpdate>(user).Any(update => update.Index == user.Index);
    }

    // Two connections of one seat (a return before the start, the old one not dropped yet) are one expected user.
    [Fact]
    public void ExpectedUsers_ReturnBeforeStart_CountsOnce()
    {
        var session = TestSession.Create(expectedUsers: 2, reconnectWindow: TimeSpan.FromSeconds(60));
        var userId = Guid.NewGuid();
        session.ReserveAndEnter(userId);

        var returned = session.Enter(userId);

        returned.Should().NotBeNull();
        session.RunTask.IsCompleted.Should().BeFalse();
        TestSession.Sent(returned!).Should().BeEmpty();
    }

    // The start is remembered: a wait that begins after it (ExpectedUsers 0 starts in the first queue action) is over
    // at once.
    [Fact]
    public void AllUsersConnected_WaitAfterTheStart_IsCompleted()
    {
        var session = TestSession.Create();

        session.Session.AllUsersConnected.IsCompletedSuccessfully.Should().BeTrue();
    }
}
