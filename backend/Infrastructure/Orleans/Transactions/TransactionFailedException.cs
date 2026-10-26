namespace Infrastructure;

// Thrown by the value-returning transaction helpers when the transaction did not commit.
// InnerException is the original failure: the action exception or the commit error.
public class TransactionFailedException : Exception
{
    public TransactionFailedException(Exception error)
        : base($"Transaction failed: {error.Message}", error)
    {
    }
}


// Thrown by Transactions.Process when the commit call failed and the database could not tell whether the transaction
// committed: not a failure to retry, the change may be in the database.
public class TransactionOutcomeUnknownException : Exception
{
    public TransactionOutcomeUnknownException(string transactionXid, Exception error)
        : base($"Outcome of database transaction {transactionXid} is unknown: {error.Message}", error)
    {
    }
}
