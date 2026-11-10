using Cysharp.Threading.Tasks;
using Shared;

namespace Global.MatchMetrics
{
    // The requests of the streams. Null answers: no answer (no connection, a loss, a timeout, an error of the server) - the
    // batch waits for the next try. Every call ends.
    public interface IMatchMetricsTransport
    {
        // Another value after every new connection, null while there is none: a stream opened on one link is opened again on
        // the next. Compared by reference.
        object Link { get; }

        UniTask<SharedMatchMetrics.Opened> Open(SharedMatchMetrics.Open open);
        UniTask<SharedMatchMetrics.BatchResult> Send(SharedMatchMetrics.Batch batch);
    }
}
