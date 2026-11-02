using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// Which transaction holds a grain: the lock, the wait for it and the takeover of a stuck transaction.
internal class GrainTransactionOwner
{
    public GrainTransactionOwner(ITransactionConfig transactionConfig, ILogger logger, Action rollbackStuck)
    {
        _transactionConfig = transactionConfig;
        _logger = logger;
        _rollbackStuck = rollbackStuck;
    }

    // Mutex held for the entire duration of an active transaction.
    // Initial count 1 = grain is free (unlocked).
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ITransactionConfig _transactionConfig;
    private readonly ILogger _logger;
    private readonly Action _rollbackStuck;

    // Last activity time of the current transaction.
    // Refreshed on every Join() call (including idempotent re-joins) and on Seal().
    // Used to detect stuck transactions eligible for takeover.
    private DateTime _currentTransactionTime;

    // Id of the transaction currently holding this grain.
    // Guid.Empty means the grain is free.
    public Guid Current { get; private set; }

    public async Task Join(Guid transactionId)
    {
        // Same transaction calling again (e.g. multiple [Transaction] methods in one Process).
        // Refresh timestamp so the stuck-transaction check stays accurate.
        if (Current == transactionId)
        {
            Refresh();
            return;
        }

        // Grain is free — take the lock at once and register the new transaction. An empty Current does not say that:
        // a release passes the lock to a waiter, which registers itself in a later turn.
        if (_lock.Wait(0) == true)
        {
            Take(transactionId);
            return;
        }

        // Another transaction is active. Wait for it to finish.
        // The previous transaction releases the lock in OnSuccess/OnFailure.
        var options = _transactionConfig.Value.ThrowIfNull();
        var isAcquired = await _lock.WaitAsync(TimeSpan.FromSeconds(options.LockWaitSeconds));

        if (isAcquired == false)
        {
            await TakeOverOrThrow(transactionId, options);
            return;
        }

        // Acquired the lock normally — the previous transaction completed while we waited.
        // Sanity check: Current should be Guid.Empty at this point
        // (OnSuccess/OnFailure always clear it before releasing the lock).
        if (Current != Guid.Empty && Current != transactionId)
        {
            _lock.Release();

            throw new Exception(
                $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{Current}'.");
        }

        Take(transactionId);
    }

    public void Refresh()
    {
        _currentTransactionTime = DateTime.UtcNow;
    }

    public void Release()
    {
        Current = Guid.Empty;

        if (_lock.CurrentCount == 0)
            _lock.Release();
    }

    private async Task TakeOverOrThrow(Guid transactionId, TransactionOptions options)
    {
        // Timed out. If the stuck transaction is still within its grace period,
        // we cannot take over — it may still be running normally but slowly.
        if (_currentTransactionTime.AddSeconds(options.StuckGraceSeconds) >= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "[Transaction] [Join] Timeout waiting for lock. TransactionId={TransactionId} BlockedBy={BlockedBy}",
                transactionId, Current);

            throw new Exception(
                $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{Current}'.");
        }

        // The transaction has been inactive for longer than StuckGraceSeconds — treat it as stuck.
        // Force-release its semaphore and acquire for ourselves.
        // The stuck transaction's eventual OnSuccess/OnFailure will fail the ID check
        // and exit early without touching the semaphore or state.
        var stuckAge = (DateTime.UtcNow - _currentTransactionTime).TotalSeconds;

        _logger.LogWarning(
            "[Transaction] [Takeover] Forcing takeover of stuck transaction. StuckTransactionId={StuckId} AgeSeconds={AgeSeconds} IncomingTransactionId={TransactionId}",
            Current, stuckAge, transactionId);

        if (_lock.CurrentCount == 0)
            _lock.Release();

        await _lock.WaitAsync();

        // Roll back any in-memory state left by the stuck transaction. A rollback that throws leaves the grain to the
        // stuck transaction: the join fails with the reason, the next one tries again.
        _rollbackStuck();

        Take(transactionId);
    }

    private void Take(Guid transactionId)
    {
        Current = transactionId;
        Refresh();
    }
}
