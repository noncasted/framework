using System.Collections.Concurrent;
using Common;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.Startup;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

/// <summary>
/// A SideEffectsWorker of its own over the database of the test cluster (the fixtures have no worker): an effect
/// running longer than the stuck threshold, the shutdown while the effects are disabled, a payload that does not
/// deserialize.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class SideEffectsWorkerTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task LongRunningEffect_NotRequeuedAsStuck()
    {
        var trackingId = Guid.NewGuid();

        // Stuck after 1.2 s, checked on every scan; the effect runs 3 s.
        var worker = CreateWorker(new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 50,
            StuckCheckIntervalSeconds = 0,
            StuckThresholdMinutes = 0.02f
        }, enabled: true);

        await GetSiloService<ISideEffectsStorage>().Write(new SlowSideEffect { TrackingId = trackingId });
        await worker.StartAsync(TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        SlowSideEffect.Executions.GetValueOrDefault(trackingId).Should()
                      .Be(1, "a running effect is not stuck: running it again in parallel duplicates it");
    }

    [Fact]
    public async Task Stop_WhileDisabled_LoopEnds()
    {
        var flags = Substitute.For<IClusterFlags>();
        var checks = 0;

        flags.SideEffectsEnabled.Returns(_ => {
            Interlocked.Increment(ref checks);
            return false;
        });

        var worker = CreateWorker(new SideEffectsOptions(), flags);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(700, TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        var afterStop = Volatile.Read(ref checks);
        await Task.Delay(1500, TestContext.Current.CancellationToken);

        Volatile.Read(ref checks).Should().Be(afterStop, "the loop of a stopped worker must end");
    }

    [Fact]
    public async Task UnknownPayloadType_DeadLetterKeepsOriginalError()
    {
        await using (var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                                  INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
                                  VALUES (@id, '{"$type":"Missing.Effect, Missing.Assembly"}'::jsonb, 0, now())
                                  """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        // No retries: the first failure goes to the dead letter.
        var worker = CreateWorker(new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 50,
            MaxRetryCount = 0
        }, enabled: true);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        var error = await WaitDeadLetterError();
        await worker.StopAsync(TestContext.Current.CancellationToken);

        error.Should().Contain("Missing.Effect", "the dead letter must keep the deserialization error");
    }

    private async Task<string> WaitDeadLetterError()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT error_message FROM side_effects_dead_letter LIMIT 1";

            if (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is string message)
                return message;

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("No dead letter entry");
    }

    private SideEffectsWorker CreateWorker(SideEffectsOptions options, bool enabled)
    {
        var flags = Substitute.For<IClusterFlags>();
        flags.SideEffectsEnabled.Returns(enabled);
        return CreateWorker(options, flags);
    }

    private SideEffectsWorker CreateWorker(SideEffectsOptions options, IClusterFlags flags)
    {
        var config = Substitute.For<ISideEffectsConfig>();
        config.Value.Returns(options);

        var loopObserver = Substitute.For<IServiceLoopObserver>();
        loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));

        var participant = Substitute.For<IClusterParticipantContext>();
        participant.IsInitialized.Returns(new ViewableProperty<bool>(true));

        return new SideEffectsWorker(
            GetSiloService<ISideEffectsStorage>(),
            GetSiloService<ITransactions>(),
            GetSiloService<IOrleans>(),
            loopObserver,
            config,
            flags,
            participant,
            NullLogger<SideEffectsWorker>.Instance);
    }
}

[GenerateSerializer]
public class SlowSideEffect : ISideEffect
{
    public static readonly ConcurrentDictionary<Guid, int> Executions = new();

    [Id(0)] public Guid TrackingId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        Executions.AddOrUpdate(TrackingId, 1, (_, count) => count + 1);
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}
