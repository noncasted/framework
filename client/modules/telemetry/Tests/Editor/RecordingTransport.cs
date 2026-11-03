using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry.Tests
{
    // Answers at once, with the ids a server would give: the first session is 1, the second is 2.
    public class RecordingTransport : ITelemetryTransport
    {
        private readonly Queue<TelemetrySendOutcome> _starts = new Queue<TelemetrySendOutcome>();
        private readonly Queue<TelemetrySendOutcome> _batches = new Queue<TelemetrySendOutcome>();
        private readonly Queue<TelemetrySendOutcome> _startups = new Queue<TelemetrySendOutcome>();
        private int _issued;
        private int _issuedStartups;

        public List<TelemetryCall> Calls { get; } = new List<TelemetryCall>();
        public IReadOnlyList<TelemetryCall> Starts => Calls.Where(call => call.Kind == TelemetryCallKind.Start).ToArray();
        public IReadOnlyList<TelemetryCall> Batches => Calls.Where(call => call.Kind == TelemetryCallKind.Batch).ToArray();
        public IReadOnlyList<TelemetryCall> Ends => Calls.Where(call => call.Kind == TelemetryCallKind.End).ToArray();

        public IReadOnlyList<TelemetryStartupTrace> Startups =>
            Calls.Where(call => call.Kind == TelemetryCallKind.Startup).Select(call => call.StartupTrace).ToArray();

        public object Link { get; private set; } = new object();

        public static Guid StartupIdOf(int record)
        {
            return new Guid(record, 1, 1, new byte[8]);
        }

        public void CloseLink()
        {
            Link = null;
        }

        public void OpenLink()
        {
            Link = new object();
        }

        public void AnswerStartup(TelemetrySendOutcome outcome)
        {
            _startups.Enqueue(outcome);
        }

        public UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace)
        {
            Calls.Add(new TelemetryCall(TelemetryCallKind.Startup, Guid.Empty, null, null, trace));

            var outcome = _startups.Count > 0 ? _startups.Dequeue() : TelemetrySendOutcome.Accepted;

            if (outcome != TelemetrySendOutcome.Accepted)
                return UniTask.FromResult(new TelemetryStartupAnswer(outcome, Guid.Empty));

            var id = trace.StartupId == Guid.Empty ? StartupIdOf(++_issuedStartups) : trace.StartupId;

            return UniTask.FromResult(new TelemetryStartupAnswer(outcome, id));
        }

        public static Guid IdOf(int session)
        {
            return new Guid(session, 0, 0, new byte[8]);
        }

        public IReadOnlyList<TelemetryBatch> BatchesOf(int session)
        {
            return Batches.Where(call => call.SessionId == IdOf(session)).Select(call => call.Batch).ToArray();
        }

        public void AnswerStart(TelemetrySendOutcome outcome)
        {
            _starts.Enqueue(outcome);
        }

        public void AnswerBatch(TelemetrySendOutcome outcome)
        {
            _batches.Enqueue(outcome);
        }

        public UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start)
        {
            var outcome = _starts.Count > 0 ? _starts.Dequeue() : TelemetrySendOutcome.Accepted;

            if (outcome != TelemetrySendOutcome.Accepted)
            {
                Calls.Add(new TelemetryCall(TelemetryCallKind.Start, Guid.Empty, start, null));
                return UniTask.FromResult(TelemetrySessionAnswer.Refused(outcome));
            }

            var id = IdOf(++_issued);
            Calls.Add(new TelemetryCall(TelemetryCallKind.Start, id, start, null));
            return UniTask.FromResult(TelemetrySessionAnswer.Accepted(id));
        }

        public UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch)
        {
            Calls.Add(new TelemetryCall(TelemetryCallKind.Batch, sessionId, null, batch));
            return UniTask.FromResult(_batches.Count > 0 ? _batches.Dequeue() : TelemetrySendOutcome.Accepted);
        }

        public UniTask End(Guid sessionId)
        {
            Calls.Add(new TelemetryCall(TelemetryCallKind.End, sessionId, null, null));
            return UniTask.CompletedTask;
        }
    }
}
