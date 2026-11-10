using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Global.MatchMetrics
{
    // The sending of one stream: one request in flight, the head of the buffer first. The stream is opened on every new link
    // of the transport (the same writer, so the server gives the same stream and its last Seq); a batch without an answer
    // stays and goes again with its Seq. A link that failed is not tried again until a new batch or a new link. Closed,
    // NotInMatch and Rejected end the sending for good.
    public class MatchMetricsSender
    {
        public MatchMetricsSender(IMatchMetricsTransport transport, IMatchMetricsTicks ticks, MatchMetricsBuffer buffer, SharedMatchMetrics.Open open)
        {
            _transport = transport;
            _ticks = ticks;
            _buffer = buffer;
            _open = open;
        }

        private readonly IMatchMetricsTransport _transport;
        private readonly IMatchMetricsTicks _ticks;
        private readonly MatchMetricsBuffer _buffer;
        private readonly SharedMatchMetrics.Open _open;

        private object _openedLink;
        private object _failedLink;
        private Guid _streamId;
        private bool _isSending;

        public bool IsStopped { get; private set; }

        // Nothing more to do: stopped, or every batch confirmed and nothing in flight.
        public bool IsIdle => _isSending == false && (IsStopped || _buffer.IsEmpty);

        public void Enqueue(MatchMetricsBatchRecord batch)
        {
            if (IsStopped)
                return;

            _buffer.Add(batch);
            _failedLink = null;
            Pump();
        }

        public void Pump()
        {
            if (_isSending || IsStopped || _buffer.IsEmpty)
                return;

            var link = _transport.Link;

            if (link == null || link == _failedLink)
                return;

            Send(link).NoAwait();
        }

        // The top of a task nobody awaits: a failure of the network is a null answer, anything else is a bug - logged, the
        // sending of this stream stops, the game goes on.
        private async UniTask Send(object link)
        {
            _isSending = true;

            try
            {
                while (IsStopped == false && _buffer.IsEmpty == false && _transport.Link == link)
                {
                    if (_openedLink != link)
                    {
                        if (await OpenOn(link) == false)
                            return;

                        continue;
                    }

                    var batch = _buffer.TakeHead();
                    var result = await _transport.Send(MatchMetricsMessages.ToBatch(_streamId, batch, _ticks.NowMs));

                    if (result == null)
                    {
                        _failedLink = link;
                        return;
                    }

                    OnResult(result.Status, batch.Seq);
                }
            }
            catch (Exception exception)
            {
                Stop();
                Debug.LogException(exception);
            }
            finally
            {
                _isSending = false;
            }
        }

        private async UniTask<bool> OpenOn(object link)
        {
            var opened = await _transport.Open(_open);

            if (opened == null)
            {
                _failedLink = link;
                return false;
            }

            if (opened.Status != SharedMatchMetrics.MatchMetricsStatus.Ok)
            {
                Stop();
                Debug.LogWarning($"[MatchMetrics] The stream of the match {_open.MatchId} is not opened: {opened.Status}");
                return false;
            }

            _openedLink = link;
            _streamId = opened.StreamId;
            _buffer.Acknowledge(opened.LastSeq);

            return true;
        }

        private void OnResult(SharedMatchMetrics.MatchMetricsStatus status, int seq)
        {
            switch (status)
            {
                case SharedMatchMetrics.MatchMetricsStatus.Ok:
                case SharedMatchMetrics.MatchMetricsStatus.Duplicate:
                    _buffer.RemoveHead();
                    break;

                case SharedMatchMetrics.MatchMetricsStatus.Closed:
                    Stop();
                    Debug.Log($"[MatchMetrics] The match {_open.MatchId} is closed: the stream stops at the batch {seq}");
                    break;

                default:
                    Stop();
                    Debug.LogError($"[MatchMetrics] The batch {seq} of the match {_open.MatchId} is {status}: the stream stops");
                    break;
            }
        }

        private void Stop()
        {
            IsStopped = true;
        }
    }
}
