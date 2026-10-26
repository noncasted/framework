using Cluster.Lobbies;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

/// <summary>
/// OpenLobbiesWatcher over a substituted registry and an in-memory channel: a gap of the channel (missed pushes)
/// reloads the open lobbies from the registry.
/// </summary>
public class OpenLobbiesWatcherTests
{
    [Fact]
    public async Task GapDetected_ReloadsFromTheRegistry()
    {
        var deployId = Guid.NewGuid();
        var registry = Substitute.For<ILobbyRegistry>();

        registry.GetOpenLobbies()
                .Returns(Task.FromResult(new OpenLobbies { Version = 1 }),
                    Task.FromResult(new OpenLobbies { Version = 3 }));

        var orleans = Substitute.For<IOrleans>();
        orleans.Grains.GetGrain<ILobbyRegistry>(deployId).Returns(registry);

        var channel = new InMemoryChannelClient();
        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Returns(channel);

        var watcher = new OpenLobbiesWatcher(orleans, messaging, NullLogger<OpenLobbiesWatcher>.Instance);
        await watcher.OnDeployChanged(deployId, new Lifetime());

        watcher.Lobbies.Value.Version.Should().Be(1);

        channel.ReportGap();

        await WaitFor(() => watcher.Lobbies.Value.Version == 3);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (condition() == false)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met");

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
