using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;

namespace Global.MatchMetrics.Tests
{
    // Holds every answer until the test gives it.
    public class FakeMatchTransport : IMatchMetricsTransport
    {
        private readonly Queue<UniTaskCompletionSource<SharedMatchMetrics.Opened>> _opens = new();
        private readonly Queue<UniTaskCompletionSource<SharedMatchMetrics.BatchResult>> _sends = new();

        public object Link { get; private set; } = new object();
        public List<SharedMatchMetrics.Open> Opens { get; } = new();
        public List<SharedMatchMetrics.Batch> Batches { get; } = new();
        public int InFlight => _opens.Count + _sends.Count;

        public UniTask<SharedMatchMetrics.Opened> Open(SharedMatchMetrics.Open open)
        {
            Opens.Add(open);
            var answer = new UniTaskCompletionSource<SharedMatchMetrics.Opened>();
            _opens.Enqueue(answer);
            return answer.Task;
        }

        public UniTask<SharedMatchMetrics.BatchResult> Send(SharedMatchMetrics.Batch batch)
        {
            Batches.Add(batch);
            var answer = new UniTaskCompletionSource<SharedMatchMetrics.BatchResult>();
            _sends.Enqueue(answer);
            return answer.Task;
        }

        public void AnswerOpen(int lastSeq = 0, SharedMatchMetrics.MatchMetricsStatus status = SharedMatchMetrics.MatchMetricsStatus.Ok)
        {
            _opens.Dequeue().TrySetResult(new SharedMatchMetrics.Opened { Status = status, StreamId = StreamId, LastSeq = lastSeq });
        }

        public void AnswerBatch(SharedMatchMetrics.MatchMetricsStatus status = SharedMatchMetrics.MatchMetricsStatus.Ok)
        {
            _sends.Dequeue().TrySetResult(new SharedMatchMetrics.BatchResult { Status = status });
        }

        // No answer, the link stays: a timeout.
        public void FailBatch()
        {
            _sends.Dequeue().TrySetResult(null);
        }

        // The connection is lost: what is in flight gets no answer, a new link comes.
        public void Reconnect()
        {
            while (_opens.Count > 0)
                _opens.Dequeue().TrySetResult(null);

            while (_sends.Count > 0)
                _sends.Dequeue().TrySetResult(null);

            Link = new object();
        }

        public static readonly System.Guid StreamId = new(7, 0, 0, new byte[8]);
    }
}
