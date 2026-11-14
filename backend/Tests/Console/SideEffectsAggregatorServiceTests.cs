using Cluster.Discovery;
using Cluster.Monitoring;
using Console;
using Framework.Tests.Fixtures;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Console;

public class SideEffectsAggregatorServiceTests
{
    [Fact]
    public async Task AServiceThatDoesNotAnswer_IsListedAsFailed()
    {
        var answering = Service("answering");
        var silent = Service("silent");

        var discovery = Substitute.For<IServiceDiscovery>();
        discovery.Entries.Returns(new Dictionary<Guid, IServiceOverview> { [answering.Id] = answering, [silent.Id] = silent });

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimePipe
                 .Send<SideEffectsSnapshotResponse>(Arg.Is<IRuntimePipeId>(id => id.ToRaw().Contains(answering.Id.ToString())), Arg.Any<object>())
                 .Returns(new SideEffectsSnapshotResponse { ServiceTag = "answering", ServiceId = answering.Id, Data = new SideEffectsLiveData { QueueCount = 3 } });
        messaging.RuntimePipe
                 .Send<SideEffectsSnapshotResponse>(Arg.Is<IRuntimePipeId>(id => id.ToRaw().Contains(silent.Id.ToString())), Arg.Any<object>())
                 .Returns<Task<SideEffectsSnapshotResponse>>(_ => throw new TimeoutException("no answer"));

        var service = new SideEffectsAggregatorService(messaging, discovery, NullLogger<SideEffectsAggregatorService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.Until(() => service.Aggregate.Value.UpdatedAt != default);
        await service.StopAsync(TestContext.Current.CancellationToken);

        var aggregate = service.Aggregate.Value;
        Assert.Equal(2, aggregate.Services.Count);
        Assert.Equal(3, aggregate.QueueCount);
        Assert.Contains(aggregate.Services, entry => entry.ServiceId == silent.Id && entry.ServiceTag == "silent");
    }

    private static ServiceOverview Service(string tag)
    {
        return new ServiceOverview { Id = Guid.NewGuid(), Tag = new ServiceTag(tag), UpdateTime = DateTime.UtcNow };
    }
}
