namespace Infrastructure;

// Delivery is at least once: an effect runs again when its worker dies before recording the result (the entry is
// requeued after SideEffectsOptions.StuckThresholdMinutes without a touch of its worker), when recording the result
// fails, or on every retry after a failure. Execute must be idempotent.
public interface ISideEffect
{
    Task Execute(IOrleans orleans);
}

// Marker interface. Implementations are executed inside Transactions.Process().
// Deletion from side_effects_processing is atomic with the transaction's Postgres commit.
public interface ITransactionalSideEffect : ISideEffect
{
}