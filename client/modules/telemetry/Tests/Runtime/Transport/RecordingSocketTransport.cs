using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry.Tests.Transport
{
    // The real transport with a note of everything that went through it: what is read from the API is compared with this.
    public class RecordingSocketTransport : ITelemetryTransport
    {
        public RecordingSocketTransport(ITelemetryTransport inner)
        {
            _inner = inner;
        }

        private readonly ITelemetryTransport _inner;

        public List<TelemetrySessionStart> Starts { get; } = new List<TelemetrySessionStart>();
        public List<TelemetrySessionAnswer> StartAnswers { get; } = new List<TelemetrySessionAnswer>();
        public List<(Guid Session, TelemetryBatch Batch, TelemetrySendOutcome Outcome)> Sent { get; } = new List<(Guid, TelemetryBatch, TelemetrySendOutcome)>();
        public List<Guid> Ends { get; } = new List<Guid>();
        public List<(TelemetryStartupTrace Trace, TelemetryStartupAnswer Answer)> Startups { get; } = new List<(TelemetryStartupTrace, TelemetryStartupAnswer)>();

        public object Link => _inner.Link;

        public async UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start)
        {
            Starts.Add(start);

            var answer = await _inner.Start(start);

            StartAnswers.Add(answer);

            return answer;
        }

        public async UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch)
        {
            var outcome = await _inner.Send(sessionId, batch);

            Sent.Add((sessionId, batch, outcome));

            return outcome;
        }

        public async UniTask End(Guid sessionId)
        {
            await _inner.End(sessionId);

            Ends.Add(sessionId);
        }

        public async UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace)
        {
            var answer = await _inner.SendStartup(trace);

            Startups.Add((trace, answer));

            return answer;
        }
    }
}
