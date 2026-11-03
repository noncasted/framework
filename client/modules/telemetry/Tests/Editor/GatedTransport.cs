using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry.Tests
{
    // Holds every answer back until the test gives it: a request in flight. Counts how many were in flight at once.
    public class GatedTransport : ITelemetryTransport
    {
        private readonly Queue<Action<TelemetrySendOutcome>> _waiting = new Queue<Action<TelemetrySendOutcome>>();
        private int _issued;
        private int _inFlight;

        public List<TelemetryCall> Calls { get; } = new List<TelemetryCall>();
        public int MaxInFlight { get; private set; }
        public int InFlight => _inFlight;

        public UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start)
        {
            var id = RecordingTransport.IdOf(++_issued);
            var answer = new UniTaskCompletionSource<TelemetrySessionAnswer>();

            Enter(new TelemetryCall(TelemetryCallKind.Start, id, start, null), outcome =>
                answer.TrySetResult(outcome == TelemetrySendOutcome.Accepted
                    ? TelemetrySessionAnswer.Accepted(id)
                    : TelemetrySessionAnswer.Refused(outcome)));

            return answer.Task;
        }

        public UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch)
        {
            var answer = new UniTaskCompletionSource<TelemetrySendOutcome>();

            Enter(new TelemetryCall(TelemetryCallKind.Batch, sessionId, null, batch), outcome => answer.TrySetResult(outcome));

            return answer.Task;
        }

        public UniTask End(Guid sessionId)
        {
            var answer = new UniTaskCompletionSource();

            Enter(new TelemetryCall(TelemetryCallKind.End, sessionId, null, null), _ => answer.TrySetResult());

            return answer.Task;
        }

        public object Link { get; } = new object();

        public UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace)
        {
            var id = RecordingTransport.StartupIdOf(1);
            var answer = new UniTaskCompletionSource<TelemetryStartupAnswer>();

            Enter(
                new TelemetryCall(TelemetryCallKind.Startup, Guid.Empty, null, null, trace),
                outcome => answer.TrySetResult(new TelemetryStartupAnswer(outcome, id)));

            return answer.Task;
        }

        public void Answer(TelemetrySendOutcome outcome = TelemetrySendOutcome.Accepted)
        {
            _inFlight--;
            _waiting.Dequeue()(outcome);
        }

        private void Enter(TelemetryCall call, Action<TelemetrySendOutcome> answer)
        {
            Calls.Add(call);
            _waiting.Enqueue(answer);
            _inFlight++;
            MaxInFlight = Math.Max(MaxInFlight, _inFlight);
        }
    }
}
