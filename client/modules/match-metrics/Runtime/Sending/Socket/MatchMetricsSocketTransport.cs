using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.MatchMetrics
{
    // The requests on the meta socket. The boundary of the failures of the network: a request without an answer is null here.
    public class MatchMetricsSocketTransport : IMatchMetricsTransport
    {
        public MatchMetricsSocketTransport(IMatchMetricsSocket socket)
        {
            _socket = socket;
        }

        private readonly IMatchMetricsSocket _socket;

        // The run of the writer, not the connection: the meta socket runs the same connection again after a loss.
        public object Link
        {
            get
            {
                var writer = _socket.Connection.Writer;

                return writer.IsClosed ? null : writer.CurrentRun;
            }
        }

        public UniTask<SharedMatchMetrics.Opened> Open(SharedMatchMetrics.Open open)
        {
            return Request<SharedMatchMetrics.Opened>(open);
        }

        public UniTask<SharedMatchMetrics.BatchResult> Send(SharedMatchMetrics.Batch batch)
        {
            return Request<SharedMatchMetrics.BatchResult>(batch);
        }

        // A closed connection, a loss, a silent server, a meta that refuses or does not know the request are what a network does:
        // null, the batch waits. An answer of another type or a request missing from the union is a bug and goes up.
        private async UniTask<T> Request<T>(INetworkContext request) where T : class
        {
            var writer = _socket.Connection.Writer;

            if (writer.IsClosed)
                return null;

            try
            {
                return await writer.WriteRequest<T>(request);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (TimeoutException)
            {
                return null;
            }
            catch (NetworkRequestException)
            {
                return null;
            }
        }
    }
}
