using Cluster.Deploy;
using Cluster.Monitoring;
using FluentAssertions;
using Game.Hosting;
using Game.Session;
using Internal;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Hosting;

// The live sessions of a game server as the cluster sees them (LiveSessionsData): the sessions change their users in
// their own queues, on many threads at once, and the slice of the server ends with the latest state.
public class SessionsCollectionTests
{
    // The queue of one session has built its snapshot and has not sent it yet when another session changes its
    // users and sends a newer one: the older snapshot must not be the last one sent.
    [Fact]
    public async Task PushesFromTwoSessionQueues_TheLatestStateIsSentLast()
    {
        var slices = new List<List<LiveSessionEntry>>();
        var liveData = Substitute.For<IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>>>();

        liveData.SetSlice(Arg.Any<List<LiveSessionEntry>>()).Returns(call => {
            lock (slices)
                slices.Add(call.Arg<List<LiveSessionEntry>>());

            return Task.CompletedTask;
        });

        var time = new PausingTime();
        var collection = new SessionsCollection(liveData, time);

        var (first, firstUsers) = CreateSession();
        var (second, secondUsers) = CreateSession();
        collection.Add(first);
        collection.Add(second);

        // The queue of the first session reads the users of both sessions and pauses before the snapshot is sent:
        // the clock is read once per session, after its users.
        var firstQueue = Task.Run(() => {
            time.PauseOnCall(2);
            firstUsers.AddUser(CreateUser());
        });

        time.Paused.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        var userOfSecond = CreateUser();
        var secondQueue = Task.Run(() => secondUsers.AddUser(userOfSecond));
        await Task.WhenAny(secondQueue, Task.Delay(TimeSpan.FromMilliseconds(200)));

        time.Resume.Set();
        await Task.WhenAll(firstQueue, secondQueue);

        List<LiveSessionEntry> last;

        lock (slices)
            last = slices.Last();

        last.Single(entry => entry.Id == second.Id).UserIds.Should().Equal(userOfSecond.BackendId);
        last.Single(entry => entry.Id == first.Id).UserIds.Should().HaveCount(1);
    }

    private static (ISession, SessionUsers) CreateSession()
    {
        var users = new SessionUsers();
        var session = Substitute.For<ISession>();
        session.Id.Returns(Guid.NewGuid());
        session.Users.Returns(users);
        session.Lifetime.Returns(new Lifetime());

        return (session, users);
    }

    private static IUser CreateUser()
    {
        var user = Substitute.For<IUser>();
        user.BackendId.Returns(Guid.NewGuid());
        user.Lifetime.Returns(new Lifetime());
        return user;
    }

    // Pauses the thread that armed it on its n-th read of the clock, until Resume.
    private class PausingTime : TimeProvider
    {
        public readonly ManualResetEventSlim Paused = new();
        public readonly ManualResetEventSlim Resume = new();

        private readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private int _thread = -1;
        private int _pauseOnCall;
        private int _calls;

        public void PauseOnCall(int call)
        {
            _pauseOnCall = call;
            _calls = 0;
            _thread = Environment.CurrentManagedThreadId;
        }

        public override DateTimeOffset GetUtcNow()
        {
            if (Environment.CurrentManagedThreadId == _thread && ++_calls == _pauseOnCall)
            {
                Paused.Set();
                Resume.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            }

            return _now;
        }
    }
}
