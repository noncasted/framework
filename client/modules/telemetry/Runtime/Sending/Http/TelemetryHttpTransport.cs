using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Global.Telemetry
{
    // The telemetry as JSON requests over HTTP (TelemetryHttpMessages). The boundary of the failures of the network: a request that
    // gets no answer becomes Failed here, with a warning, and the link is replaced: the server may have taken the request or not,
    // so the old link counts as a lost socket (its session closes as lost when the server stops hearing it). Nothing is queued or
    // repeated; the timeout is the one of ITelemetryHttp.
    public class TelemetryHttpTransport : ITelemetryTransport
    {
        public TelemetryHttpTransport(ITelemetryHttp http)
        {
            _http = http;
        }

        private readonly ITelemetryHttp _http;

        private TelemetryHttpLink _link = new();

        public object Link => _link;

        public async UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start)
        {
            var answer = await Request<TelemetryHttpSessionStarted>("/start", TelemetryHttpMessages.ToSessionStart(_link.Id, start));

            if (answer == null)
                return TelemetrySessionAnswer.Refused(TelemetrySendOutcome.Failed);

            return answer.enabled
                ? TelemetrySessionAnswer.Accepted(Guid.Parse(answer.sessionId))
                : TelemetrySessionAnswer.Refused(TelemetrySendOutcome.Disabled);
        }

        public async UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch)
        {
            var answer = await Request<TelemetryHttpBatchResult>("/batch", TelemetryHttpMessages.ToBatch(_link.Id, sessionId, batch));

            if (answer == null)
                return TelemetrySendOutcome.Failed;

            return answer.enabled ? TelemetrySendOutcome.Accepted : TelemetrySendOutcome.Disabled;
        }

        // A failure here has nobody to tell: the link is replaced, the server closes the session of the old one.
        public async UniTask End(Guid sessionId)
        {
            await Request<TelemetryHttpBatchResult>("/end", TelemetryHttpMessages.ToSessionEnd(_link.Id, sessionId));
        }

        public async UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace)
        {
            var answer = await Request<TelemetryHttpStartupResult>("/startup", TelemetryHttpMessages.ToStartupTrace(_link.Id, trace));

            if (answer == null)
                return new TelemetryStartupAnswer(TelemetrySendOutcome.Failed, Guid.Empty);

            return answer.enabled
                ? new TelemetryStartupAnswer(TelemetrySendOutcome.Accepted, Guid.Parse(answer.startupId))
                : new TelemetryStartupAnswer(TelemetrySendOutcome.Disabled, Guid.Empty);
        }

        // Null: no answer. An answer that is not the JSON of the contract is a bug and goes up to the session, which writes its line.
        private async UniTask<T> Request<T>(string path, object message) where T : class
        {
            string answer;

            try
            {
                answer = await _http.Post(path, JsonUtility.ToJson(message));
            }
            catch (TelemetryHttpException exception)
            {
                Debug.LogWarning($"[Telemetry] {path} failed: {exception.Message}");
                _link = new TelemetryHttpLink();
                return null;
            }

            return JsonUtility.FromJson<T>(answer);
        }
    }
}
