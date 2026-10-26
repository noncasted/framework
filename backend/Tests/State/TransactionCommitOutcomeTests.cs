using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Framework.Tests.State;

/// <summary>
/// A commit whose call fails after the server committed (the connection dropped while the answer was on the way):
/// the transaction is done and must not be reported as failed, a caller would apply it again.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionCommitOutcomeTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task CommitThrowsAfterServerCommitted_ReportedAsSuccess()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = CreateTransactions(commitThenThrow: true);

        var result = await transactions.Run(() => grain.Increment());

        result.IsSuccess.Should().BeTrue("the change is in the database: a failure result makes the caller apply it again");

        await grain.Deactivate();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        (await grain.Get()).Should().Be(1);
    }

    [Fact]
    public async Task CommitFailsBeforeServerCommitted_ReportedAsFailure()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = CreateTransactions(commitThenThrow: false);

        var result = await transactions.Run(() => grain.Increment());

        result.IsSuccess.Should().BeFalse();
        (await grain.Get()).Should().Be(0);
    }

    private FaultyCommitTransactions CreateTransactions(bool commitThenThrow)
    {
        return new FaultyCommitTransactions(
            commitThenThrow,
            GetSiloService<IStateStorage>(),
            GetSiloService<IEventStorage>(),
            GetSiloService<IDbSource>(),
            GetSiloService<ISideEffectsStorage>(),
            GetSiloService<ITransactionConfig>());
    }

    private class FaultyCommitTransactions(
        bool commitThenThrow,
        IStateStorage stateStorage,
        IEventStorage eventStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        ITransactionConfig transactionConfig)
        : Transactions(stateStorage, eventStorage, dbSource, sideEffectsStorage, transactionConfig,
            NullLogger<Transactions>.Instance)
    {
        protected override async Task CommitTransaction(NpgsqlTransaction transaction)
        {
            if (commitThenThrow == true)
                await transaction.CommitAsync();
            else
                await transaction.RollbackAsync();

            throw new NpgsqlException("Exception while reading from stream");
        }
    }
}
