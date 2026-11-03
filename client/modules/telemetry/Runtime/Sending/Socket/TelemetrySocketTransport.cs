using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Telemetry
{
    // The telemetry as requests on the meta socket. The boundary of the failures of the network: a request that gets no answer
    // becomes Failed here. Nothing is queued or repeated. Every call ends, by the timeout of the writer at the latest: this
    // has no timer of its own, one shorter than the writer's would give up on an answer still on its way.
    //
    // Keeps nothing between the calls, so a request outside a session needs nothing from it.
    public class TelemetrySocketTransport : ITelemetryTransport
    {
        public TelemetrySocketTransport(ITelemetrySocket socket)
        {
            _socket = socket;
        }

        private readonly ITelemetrySocket _socket;

        public async UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start)
        {
            var started = await Request<SharedClientTelemetry.SessionStarted>(TelemetryMessages.ToSessionStart(start));

            if (started == null)
                return TelemetrySessionAnswer.Refused(TelemetrySendOutcome.Failed);

            return started.Enabled
                ? TelemetrySessionAnswer.Accepted(started.SessionId)
                : TelemetrySessionAnswer.Refused(TelemetrySendOutcome.Disabled);
        }

        public async UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch)
        {
            var result = await Request<SharedClientTelemetry.BatchResult>(TelemetryMessages.ToBatch(sessionId, batch));

            if (result == null)
                return TelemetrySendOutcome.Failed;

            return result.Enabled ? TelemetrySendOutcome.Accepted : TelemetrySendOutcome.Disabled;
        }

        // A failure here has nobody to tell: the socket decides on the server, its session is closed by its close.
        public async UniTask End(Guid sessionId)
        {
            await Request<EmptyResponse>(new SharedClientTelemetry.SessionEnd { SessionId = sessionId });
        }

        // The run of the writer, not the connection: the meta socket runs the same connection again after a loss.
        public object Link
        {
            get
            {
                var writer = _socket.Connection.Writer;

                return writer.IsClosed ? null : writer.CurrentRun;
            }
        }

        public async UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace)
        {
            var result = await Request<SharedClientTelemetry.StartupTraceResult>(TelemetryMessages.ToStartupTrace(trace));

            if (result == null)
                return new TelemetryStartupAnswer(TelemetrySendOutcome.Failed, Guid.Empty);

            return result.Enabled
                ? new TelemetryStartupAnswer(TelemetrySendOutcome.Accepted, result.StartupId)
                : new TelemetryStartupAnswer(TelemetrySendOutcome.Disabled, Guid.Empty);
        }

        // Null: no answer. A closed connection, a loss, a silent server, a meta that refuses or does not know the request are what
        // a network does, so they end the sending quietly. The rest (an answer of another type, a request missing from the union
        // of the game) is a bug and goes up to the session, which writes its line.
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
