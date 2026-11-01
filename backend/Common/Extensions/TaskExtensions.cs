namespace Common.Extensions;

// The failure of a task nobody awaits (a background loop, a fire-and-forget call) has no caller to reach: NoAwait
// hands it to Handler at the moment the task fails. ClusterParticipantStartup installs a logging one when the host has
// none; without a handler the failure goes to the standard error, never only to an unobserved task.
public static class NoAwaitExceptions
{
    public static Action<Exception>? Handler { get; private set; }

    // Null removes the handler.
    public static void SetHandler(Action<Exception>? handler)
    {
        Handler = handler;
    }

    internal static void Report(Exception exception)
    {
        var handler = Handler;

        if (handler == null)
        {
            Console.Error.WriteLine($"[NoAwait] Unobserved task failed: {exception}");
            return;
        }

        handler(exception);
    }
}

public static class TaskExtensions
{
    private static readonly Action<Task> _noAwaitContinuation = (t => {
        if (t.Exception == null || t.Exception.IsOperationCanceled())
            return;

        var exception = t.Exception.InnerExceptions.Count == 1 ? t.Exception.InnerExceptions[0] : t.Exception;
        NoAwaitExceptions.Report(exception);
    });

    public static void NoAwait(this Task? task)
    {
        task?.ContinueWith(_noAwaitContinuation,
            TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
    }

    public static bool IsOperationCanceled(this Exception? exception)
    {
        switch (exception)
        {
            case null:
                return false;
            case OperationCanceledException _:
                return true;
            case AggregateException aggregateException:
                if (aggregateException.InnerExceptions.Count == 0)
                    return false;

                foreach (Exception innerException in aggregateException.InnerExceptions)
                {
                    if (!innerException.IsOperationCanceled())
                        return false;
                }

                return true;
            default:
                return exception.InnerException.IsOperationCanceled();
        }
    }
}