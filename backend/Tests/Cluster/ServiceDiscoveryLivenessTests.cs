using Cluster.Coordination;
using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

/// <summary>
/// The liveness of the members of the service discovery: a stopping service stays unregistered, the staleness is
/// measured by one clock, a game server that stopped refreshing is not picked, the wait for the cluster does not
/// flood the log.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class ServiceDiscoveryLivenessTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Push_AfterUnregister_DoesNotRegisterAgain()
    {
        var storage = Substitute.For<IServiceDiscoveryStorage>();
        storage.Update(default!).ReturnsForAnyArgs(new Dictionary<Guid, IServiceOverview>());

        var client = Substitute.For<IClusterClient>();
        client.GetGrain<IServiceDiscoveryStorage>(Arg.Any<Guid>(), Arg.Any<string?>()).Returns(storage);

        var orleans = Substitute.For<IOrleans>();
        orleans.Client.Returns(client);

        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(Guid.NewGuid());

        var discovery = new ServiceDiscovery(orleans, deployContext,
            new ServiceEnvironment { IsDevelopment = true, Tag = TestRoles.Meta },
            Options.Create(new ServiceDiscoveryOptions()), NullLogger<ServiceDiscovery>.Instance);

        // ApplicationStopping unregisters; the refresh loop still runs until the host stops it.
        await discovery.Unregister();
        await discovery.Push();

        await storage.DidNotReceiveWithAnyArgs().Update(default!);
    }

    [Fact]
    public async Task Update_MemberClockBehind_NotRemovedAsStale()
    {
        var storage = GetGrain<IServiceDiscoveryStorage>(Guid.NewGuid());

        // The clock of the member runs a minute behind the one of the storage.
        var member = new ServiceOverview
        {
            Id = Guid.NewGuid(),
            Tag = TestRoles.Meta,
            UpdateTime = DateTime.UtcNow.AddMinutes(-1)
        };

        var members = await storage.Update(member);

        members.Should().ContainKey(member.Id, "a member that has just reported is alive");
    }

    [Fact]
    public void Pick_ServerStoppedRefreshing_NotPicked()
    {
        var now = DateTime.UtcNow;
        var alive = Server(now);
        var crashed = Server(now.AddSeconds(-20));

        var discovery = Substitute.For<IServiceDiscovery>();

        discovery.Entries.Returns(new Dictionary<Guid, IServiceOverview>
        {
            [alive.Id] = alive,
            [crashed.Id] = crashed
        });

        var data = new LiveSessionsData();
        data.Slices[alive.Id] = [new LiveSessionEntry { Id = Guid.NewGuid() }];

        var balancer = GameServerBalancerTests.Create(discovery, data);

        for (var i = 0; i < 20; i++)
            balancer.Pick().Should().BeSameAs(alive, "a server without a heartbeat for 20 s has crashed");
    }

    [Fact]
    public async Task WaitClusterReady_MissingService_WarnsOnce()
    {
        var logger = new CountingLogger();

        var loopObserver = Substitute.For<IServiceLoopObserver>();
        loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimePipe.Exists(DeployIdPipe.Id).Returns(true);
        messaging.RuntimePipe.Send<DeployIdResponse>(DeployIdPipe.Id, Arg.Any<object>())
                 .Returns(new DeployIdResponse { DeployId = Guid.NewGuid() });

        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Self.Returns(new ServiceOverview { Id = Guid.NewGuid(), Tag = TestRoles.Meta, UpdateTime = DateTime.UtcNow });
        discovery.Entries.Returns(new Dictionary<Guid, IServiceOverview>());

        var startupConfig = Substitute.For<IClusterStartupConfig>();
        startupConfig.RequiredServices.Returns([TestRoles.Silo]);

        var startup = new ClusterParticipantStartup(
            Substitute.For<ITaskBalancer>(),
            discovery,
            loopObserver,
            Substitute.For<IServiceLoop>(),
            messaging,
            Substitute.For<IOrleans>(),
            Substitute.For<IDeployContext>(),
            Substitute.For<IClusterParticipantContext>(),
            startupConfig,
            Substitute.For<IHostApplicationLifetime>(),
            logger);

        await startup.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        await startup.StopAsync(TestContext.Current.CancellationToken);

        logger.Count("cluster not ready").Should().BeLessThanOrEqualTo(1,
            "the missing services are logged when they change, not on every poll");
    }

    private static GameServerOverview Server(DateTime updateTime)
    {
        return new GameServerOverview
        {
            Id = Guid.NewGuid(),
            Tag = TestRoles.Game,
            UpdateTime = updateTime,
            Url = "http://localhost:7102"
        };
    }

    private class CountingLogger : ILogger<ClusterParticipantStartup>
    {
        private readonly List<string> _messages = [];

        public int Count(string fragment)
        {
            lock (_messages)
                return _messages.Count(message => message.Contains(fragment, StringComparison.Ordinal));
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_messages)
                _messages.Add(formatter(state, exception));
        }
    }
}
