using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

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

        private object _warnedRun;

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
        // null, the batch waits. A refusal or a silence of the server is a warning once per run of the connection (a meta
        // without the handler refuses every batch); a loss is said by the connection itself. An answer of another type or a
        // request missing from the union is a bug and goes up.
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
            catch (TimeoutException exception)
            {
                Warn(writer, request, exception);
                return null;
            }
            catch (NetworkRequestException exception)
            {
                Warn(writer, request, exception);
                return null;
            }
        }

        private void Warn(IConnectionWriter writer, INetworkContext request, Exception exception)
        {
            var run = writer.CurrentRun;

            if (run == _warnedRun)
                return;

            _warnedRun = run;
            Debug.LogWarning($"[MatchMetrics] {request.GetType().Name} got no answer, the batch waits: {exception.Message}");
        }
    }
}
