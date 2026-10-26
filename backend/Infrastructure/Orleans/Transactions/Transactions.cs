using System.Collections.Concurrent;
using System.Diagnostics;
using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure;

public class TransactionResult
{
    public required bool IsSuccess { get; init; }
    public Exception? Error { get; init; }

    public override string ToString() => Error != null
        ? $"TransactionResult(Success={IsSuccess}, Error={Error})"
        : $"TransactionResult(Success={IsSuccess})";
}

public class TransactionParameters
{
    public required Func<Task> Action { get; init; }
    public required List<Func<NpgsqlTransaction, Task>> Callbacks { get; init; }
}

public class TransactionCommitResult
{
    public required IReadOnlyList<GrainStateRecord> States { get; init; }
    public required List<GrainEventRecord> Events { get; init; }
}

public interface ITransactions
{
    Task<TransactionResult> Process(TransactionParameters action);
}

public class Transactions : ITransactions
{
    public Transactions(
        IStateStorage stateStorage,
        IEventStorage eventStorage,
        IDbSource dbSource,
        ISideEffectsStorage sideEffectsStorage,
        Lazy<ITransactionConfig> transactionConfig,
        ILogger<Transactions> logger)
    {
        _stateStorage = stateStorage;
        _eventStorage = eventStorage;
        _dbSource = dbSource;
        _sideEffectsStorage = sideEffectsStorage;
        _transactionConfig = transactionConfig;
        _logger = logger;
    }

    private readonly IStateStorage _stateStorage;
    private readonly IEventStorage _eventStorage;
    private readonly IDbSource _dbSource;
    private readonly ISideEffectsStorage _sideEffectsStorage;
    private readonly Lazy<ITransactionConfig> _transactionConfig;
    private readonly ILogger<Transactions> _logger;

    public async Task<TransactionResult> Process(TransactionParameters parameters)
    {
        using var activity = TraceExtensions.Transactions.StartActivity("Transaction.Process");
        using var watch = MetricWatch.Start(BackendMetrics.TransactionDuration);

        var context = new TransactionContext
        {
            Id = Guid.NewGuid()
        };

        activity?.SetTag("transaction.id", context.Id);

        TransactionContextProvider.SetCurrent(context);

        try
        {
            await parameters.Action();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] In action exception occured during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        TransactionCommitResult result;

        try
        {
            result = await CollectStates();
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to collect commit result during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        activity?.SetTag("participant.count", context.Participants.Count);
        BackendMetrics.TransactionParticipantCount.Record(context.Participants.Count);

        try
        {
            await Commit(result, context, parameters.Callbacks);
        }
        catch (TransactionOutcomeUnknownException e)
        {
            // Neither a success nor a failure the caller may retry: the memory of the participants is reloaded from
            // the database (whatever is there), the caller gets the exception.
            _logger.LogError(e,
                "[Transaction] [Error] Outcome of the commit of transaction {TransactionId} is unknown",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to commit to db during transaction {TransactionId}",
                context.Id);

            await Rollback(context, activity);
            BackendMetrics.TransactionTotal.Add(1);
            BackendMetrics.TransactionFailure.Add(1);

            return new TransactionResult
            {
                IsSuccess = false,
                Error = e
            };
        }

        // The changes are in the database from here on: the result is success whatever the participants do.
        // Rolling back (OnFailure) after the commit would desync grain memory from the database, and a failure
        // result would make the caller retry an operation that is already done.
        await ConfirmParticipants(context);

        BackendMetrics.TransactionTotal.Add(1);
        BackendMetrics.TransactionSuccess.Add(1);

        return new TransactionResult
        {
            IsSuccess = true
        };

        async Task<TransactionCommitResult> CollectStates()
        {
            var states = new List<GrainStateRecord>();
            var events = new List<GrainEventRecord>();

            var collections = await Task.WhenAll(context.Participants.Select(p => Collect(p.Key, p.Value)));

            foreach (var collection in collections)
            {
                states.AddRange(collection.States);
                events.AddRange(collection.Events);
            }

            return new TransactionCommitResult()
            {
                States = states,
                Events = events
            };

            async Task<TransactionCommitResult> Collect(Guid handlerId, IGrainTransactionHandler handler)
            {
                var grainStates = new List<GrainStateRecord>();
                var grainEvents = new List<GrainEventRecord>();

                // The snapshot normally arrives inside TransactionResponse (TransactionRequestBase.Invoke);
                // the RPC is only for participants whose response did not reach this context.
                var result = context.Results.TryGetValue(handlerId, out var snapshot)
                    ? snapshot
                    : await handler.CollectResult(context.Id);
                var participantId = handler.GetGrainId();

                foreach (var state in result.States)
                {
                    grainStates.Add(new GrainStateRecord
                    {
                        Id = participantId,
                        Value = state
                    });
                }

                grainEvents.AddRange(result.Events);

                return new TransactionCommitResult
                {
                    States = grainStates,
                    Events = grainEvents
                };
            }
        }
    }

    private async Task Commit(
        TransactionCommitResult result,
        TransactionContext context,
        List<Func<NpgsqlTransaction, Task>> callbacks)
    {
        await using var connection = await _dbSource.Value.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        string transactionXid;

        try
        {
            // Direct states and side effects go to Postgres as one NpgsqlBatch: one round trip
            // for the whole transaction instead of one per table group plus one for side effects.
            transactionXid = await WriteBatch(connection, transaction, result.States, context.SideEffects);

            // Marten runs its own pipeline inside the same transaction; it cannot join the batch.
            if (result.Events.Count != 0)
                await _eventStorage.Write(transaction, result.Events);

            foreach (var callback in callbacks)
                await callback(transaction);

            // A participant taken over by another transaction (GrainTransactionHandler.Join) rejects the seal: the
            // upserts carry no version, committing would overwrite the newer commit with this old snapshot.
            await Task.WhenAll(context.Participants.Values.Select(participant => participant.Seal(context.Id)));
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Transaction] [Error] Failed to record changes during transaction {TransactionId}",
                context.Id);

            await transaction.RollbackAsync();
            throw;
        }

        // A failed commit is rolled back by the server; there is nothing left to roll back here. But the call can fail
        // after the server committed (the connection broke while the answer was on the way): reported as a failure,
        // the caller would apply the change again. The server tells what happened to the transaction id.
        try
        {
            await CommitTransaction(transaction);
        }
        catch (Exception e)
        {
            if (await IsCommitted(transactionXid, e) == false)
                throw;

            _logger.LogWarning(e,
                "[Transaction] Commit of transaction {TransactionId} reported a failure, but the server committed it",
                context.Id);
        }
    }

    private async Task<bool> IsCommitted(string transactionXid, Exception commitError)
    {
        const int attempts = 50;

        try
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_xact_status(@xid::xid8)";
                command.Parameters.AddWithValue("xid", transactionXid);

                switch (await command.ExecuteScalarAsync())
                {
                    case "committed":
                        return true;
                    case "aborted":
                        return false;
                    case "in progress":
                        // The server still finishes the transaction of the broken connection.
                        await Task.Delay(100);
                        continue;
                    default:
                        throw new TransactionOutcomeUnknownException(transactionXid, commitError);
                }
            }
        }
        catch (Exception e) when (e is not TransactionOutcomeUnknownException)
        {
            throw new TransactionOutcomeUnknownException(transactionXid, new AggregateException(commitError, e));
        }

        throw new TransactionOutcomeUnknownException(transactionXid, commitError);
    }

    // The commit itself, apart for the tests: a commit that throws after the server committed.
    protected virtual Task CommitTransaction(NpgsqlTransaction transaction)
    {
        return transaction.CommitAsync();
    }

    private async Task ConfirmParticipants(TransactionContext context)
    {
        var confirmations = context.Participants.Select(async participant => {
            try
            {
                await participant.Value.OnSuccess(context.Id);
            }
            catch (Exception e)
            {
                // Not a transaction failure: the data is committed. A handler that threw has released its grain
                // already (GrainTransactionHandler.OnSuccess); one the call did not reach keeps the lock until
                // the stuck-transaction takeover in GrainTransactionHandler.Join reloads its state.
                _logger.LogError(e,
                    "[Transaction] [Error] Participant {ParticipantId} failed to confirm committed transaction {TransactionId}",
                    participant.Key, context.Id);

                BackendMetrics.TransactionConfirmFailure.Add(1);
            }
        });

        await Task.WhenAll(confirmations);
    }

    // Returns the id of the database transaction (pg_current_xact_id) for IsCommitted.
    private async Task<string> WriteBatch(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<GrainStateRecord> states,
        ConcurrentDictionary<Guid, ISideEffect> sideEffects)
    {
        // The server aborts this transaction when the commit does not follow the seal of the participants within
        // half of their stuck grace: a takeover after the grace (GrainTransactionHandler.Seal) cannot race a commit.
        var idleTimeout = (int)(_transactionConfig.Value.Value.StuckGraceSeconds * 1000 / 2);

        var commands = new List<NpgsqlBatchCommand>
        {
            new("SELECT pg_current_xact_id()::text"),
            new($"SET LOCAL idle_in_transaction_session_timeout = {idleTimeout}")
        };

        if (states.Count != 0)
            commands.AddRange(_stateStorage.BuildWriteCommands(states));

        if (sideEffects.Count != 0)
            commands.Add(_sideEffectsStorage.BuildWriteCommand(sideEffects.Values.ToList()));

        // Same metrics DirectStorage.Write would have recorded for the transactional write.
        var start = Stopwatch.GetTimestamp();

        try
        {
            await using var batch = new NpgsqlBatch(connection, transaction);

            foreach (var command in commands)
                batch.BatchCommands.Add(command);

            await using var reader = await batch.ExecuteReaderAsync();

            if (await reader.ReadAsync() == false)
                throw new InvalidOperationException("The batch of the transaction returned no transaction id");

            var transactionXid = reader.GetString(0);

            // The errors of the writes come with their results.
            while (await reader.NextResultAsync())
            {
            }

            return transactionXid;
        }
        finally
        {
            if (states.Count != 0)
            {
                BackendMetrics.StateWriteDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                BackendMetrics.StateWriteTotal.Add(1);
                BackendMetrics.StateWriteBatchSize.Record(states.Count);
            }
        }
    }

    private async Task Rollback(TransactionContext context, Activity? activity = null)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Transaction rolled back");
        BackendMetrics.TransactionRollback.Add(1);

        foreach (var (participantId, participant) in context.Participants)
        {
            try
            {
                await participant.OnFailure(context.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Transactions] Failed to rollback participant {ParticipantId}", participantId);
                BackendMetrics.TransactionRollbackFailure.Add(1);
            }
        }
    }
}