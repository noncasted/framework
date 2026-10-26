using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orleans.Core.Internal;
using Xunit;

namespace Framework.Tests.Messaging;

/// <summary>
/// Resubscribe of a channel listener: two of them in flight together (the first Listen and the resubscribe loop), and
/// a channel grain reactivated between two of them (its sequence numbers start again from 1).
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelResubscribeTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Resubscribe_Overlapping_BufferedMessageDelivered()
    {
        var channel = Substitute.For<IRuntimeChannel>();
        var firstAdd = new TaskCompletionSource();
        var addCalls = 0;

        channel.AddObserver(default, default!)
               .ReturnsForAnyArgs(_ => Interlocked.Increment(ref addCalls) == 1 ? firstAdd.Task : Task.CompletedTask);

        channel.CatchUp(default).ReturnsForAnyArgs(new CatchUpResult { CurrentSequence = 1 });

        var listener = new RuntimeChannelClient.Listener(new RuntimeChannelId("overlap"), channel,
            Substitute.For<IOrleans>(), NullLogger.Instance);

        var received = new List<object>();

        listener.AddHandler(message => {
            received.Add(message);
            return Task.CompletedTask;
        }, null);

        // The first resubscribe buffers while it binds; a message comes; a second resubscribe starts meanwhile.
        var first = listener.Resubscribe();
        await listener.ObserverSource.Send(new SequencedMessage { Sequence = 1, Payload = "a" });
        var second = listener.Resubscribe();

        firstAdd.SetResult();
        await Task.WhenAll(first, second);

        received.Should().Equal("a");
    }

    // A listener of its own, resubscribed by the test: the resubscribe loop of the silo client sleeps the shortest
    // interval of all its listeners (up to a minute once they are stable), which made a wait for it flaky in the
    // full suite.
    [Fact]
    public async Task Resubscribe_ChannelReactivated_NewMessagesNotDropped()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var orleans = GetSiloService<IOrleans>();
        var channel = GetGrain<IRuntimeChannel>(channelId.ToRaw());
        var listener = new RuntimeChannelClient.Listener(channelId, channel, orleans, NullLogger.Instance);
        var received = new List<string>();

        listener.AddHandler(message => {
            lock (received)
                received.Add(((TestMessage)message).Text);

            return Task.CompletedTask;
        }, null);

        await listener.Resubscribe();

        for (var i = 1; i <= 3; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Text = $"old-{i}" });

        await WaitFor(() => Count(received) == 3);

        // A new activation numbers its messages from 1 again: the listener has seen up to 3.
        await channel.Cast<IGrainManagementExtension>().DeactivateOnIdle();
        await Task.Delay(500, TestContext.Current.CancellationToken);

        for (var i = 1; i <= 3; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Text = $"new-{i}" });

        await listener.Resubscribe();

        await WaitFor(() => Count(received) == 6);

        lock (received)
            received.Should().Equal("old-1", "old-2", "old-3", "new-1", "new-2", "new-3");

        listener.Cleanup();
    }

    private static int Count(List<string> received)
    {
        lock (received)
            return received.Count;
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));

        while (condition() == false)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met");

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
