using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Xunit;

namespace Framework.Tests.State;

/// <summary>
/// The stuck-transaction takeover of GrainTransactionHandler.Join: a transaction taken over must not commit its old
/// snapshot over the one of the transaction that took the grain.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionTakeoverTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Takeover_StalledTransactionResumes_NoLostUpdate()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();

        // The fixture: LockWaitSeconds 2, StuckGraceSeconds 5. The first transaction joins at 0 s and stalls; the
        // second one joins at 4 s, times out at 6 s and takes the grain over; the first one resumes at 8 s.
        var stalled = transactions.Run(async () => {
            await grain.Increment();
            await Task.Delay(TimeSpan.FromSeconds(8));
        });

        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        var takeover = await transactions.Run(() => grain.Increment());
        var stalledResult = await stalled;

        takeover.IsSuccess.Should().BeTrue();

        await grain.Deactivate();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var succeeded = (stalledResult.IsSuccess ? 1 : 0) + 1;
        var value = await grain.Get();
        value.Should().Be(succeeded, "every transaction reported successful must be in the database");
    }
}
