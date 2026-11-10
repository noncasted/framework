using System.Collections.Generic;
using UnityEngine;

namespace Global.MatchMetrics
{
    // The batches of a stream the server has not confirmed, oldest first. Only the head is ever sent, so only it may have a
    // number. Over the limit the oldest batch without a number is lost (an error in the log), its declarations go on.
    public class MatchMetricsBuffer
    {
        public MatchMetricsBuffer(int capacity)
        {
            _capacity = capacity;
        }

        private readonly int _capacity;
        private readonly List<MatchMetricsBatchRecord> _batches = new();

        private int _nextSeq = 1;

        public bool IsEmpty => _batches.Count == 0;
        public int Count => _batches.Count;

        public void Add(MatchMetricsBatchRecord batch)
        {
            _batches.Add(batch);

            if (_batches.Count <= _capacity)
                return;

            var index = _batches[0].Seq == 0 ? 0 : 1;
            var dropped = _batches[index];
            _batches.RemoveAt(index);
            _batches[index].TakeSeries(dropped);

            Debug.LogError($"[MatchMetrics] The buffer is full ({_capacity} batches): the oldest unsent batch is lost");
        }

        // The head with its number: the next one at its first send, the same at every repeat.
        public MatchMetricsBatchRecord TakeHead()
        {
            var head = _batches[0];

            if (head.Seq == 0)
                head.Number(_nextSeq++);

            return head;
        }

        public void RemoveHead()
        {
            _batches.RemoveAt(0);
        }

        // The server has every batch up to lastSeq: a head it got before a loss is not sent again.
        public void Acknowledge(int lastSeq)
        {
            if (_batches.Count > 0 && _batches[0].Seq != 0 && _batches[0].Seq <= lastSeq)
                _batches.RemoveAt(0);

            if (_nextSeq <= lastSeq)
                _nextSeq = lastSeq + 1;
        }
    }
}
