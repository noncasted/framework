using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry
{
    // One session from its start to the last call on the transport. The collecting is over at once when the session closes,
    // so the next session starts clean at that moment; the sending goes on with the batches taken at the close and ends with
    // the end of the session. Only one request is in flight: a session sends after the sending of the previous one is over,
    // and one that was closed before its turn sends nothing.
    //
    // A refusal of the server or a failure ends the sending of this session for good: no repeat, nothing is kept.
    public class TelemetrySession
    {
        public TelemetrySession(
            ClientTelemetryOptions options,
            TelemetryCollection collection,
            TelemetryBatchWriter writer,
            ITelemetryTransport transport,
            ITelemetryDeviceSource device,
            TelemetryRequestGate gate,
            TelemetryStartupReport startup,
            TelemetrySessionTrace trace,
            IReadOnlyList<string> tags)
        {
            _trace = trace;
            _options = options;
            _collection = collection;
            _writer = writer;
            _transport = transport;
            _device = device;
            _gate = gate;
            _startup = startup;
            _tags = tags;
            _close = Close;
        }

        private readonly ClientTelemetryOptions _options;
        private readonly TelemetryCollection _collection;
        private readonly TelemetryBatchWriter _writer;
        private readonly ITelemetryTransport _transport;
        private readonly ITelemetryDeviceSource _device;
        private readonly TelemetryRequestGate _gate;
        private readonly TelemetryStartupReport _startup;
        private readonly TelemetrySessionTrace _trace;
        private readonly IReadOnlyList<string> _tags;
        private readonly Action _close;
        private readonly UniTaskCompletionSource _sent = new();

        private const string PreviousMark = "session.previous_unclosed";

        private IReadOnlyLifetime _lifetime;
        private ILifetime _running;
        private IReadOnlyList<TelemetryBatch> _lastBatches = Array.Empty<TelemetryBatch>();
        private TelemetryPreviousSession _previous;
        private Guid _sessionId;
        private bool _isOver;

        // Done when this session has nothing more for the transport. Never fails, and awaited only by the next session.
        public UniTask Sent => _sent.Task;

        public void Open(IReadOnlyLifetime lifetime, UniTask previousSent)
        {
            _lifetime = lifetime;
            _running = _collection.Begin();

            lifetime.Listen(_close);

            Sampling().NoAwait();
            Sending(previousSent).NoAwait();
        }

        public void Close()
        {
            if (_isOver)
                return;

            MarkOver();
            _lastBatches = _collection.End(_running);
        }

        private void Stop()
        {
            _lastBatches = Array.Empty<TelemetryBatch>();

            if (_isOver)
                return;

            MarkOver();
            _collection.Drop(_running);
        }

        // Over on the client, whatever the server got: the trace of this session has nothing to report.
        private void MarkOver()
        {
            _isOver = true;
            _lifetime.RemoveListener(_close);
            _trace.Close(_sessionId);
        }

        // The top of a task nobody awaits. The end of the session is the expected way out. Anything else is a bug here or in
        // the code of the game: one line in the log and no more telemetry in this session, the game goes on.
        private async UniTask Sampling()
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(_options.SampleInterval, DelayType.Realtime, PlayerLoopTiming.Update, _running.Token);
                    _collection.Sample();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Stop();
                TelemetrySelfLog.Exception(exception);
            }
        }

        // The top of a task nobody awaits, as above. A transport may cancel when its socket is gone.
        private async UniTask Sending(UniTask previousSent)
        {
            try
            {
                await previousSent;

                if (_isOver == false)
                    await SendSession();
            }
            catch (OperationCanceledException)
            {
                Stop();
            }
            catch (Exception exception)
            {
                Stop();
                TelemetrySelfLog.Exception(exception);
            }
            finally
            {
                _sent.TrySetResult();
            }
        }

        // The start goes at once: with the flag off it is the only call. In its turn of the gate: it reads the id of a startup trace
        // whose request came first, and a session closed while waiting sends nothing.
        private async UniTask SendSession()
        {
            var answer = await _gate.Run(StartOnTurn);

            if (answer.Outcome != TelemetrySendOutcome.Accepted)
            {
                Stop();
                return;
            }

            if (_isOver == false)
                OpenTrace(answer.SessionId);
            else if (_previous != null)
                _trace.Forget();

            while (_isOver == false)
            {
                await WaitForNextBatch();

                // Closed while waiting: what is left is in the last batches.
                if (_isOver)
                    break;

                var batch = _writer.WriteNext();

                if (batch != null && await IsRefused(answer.SessionId, batch))
                    return;
            }

            foreach (var batch in _lastBatches)
            {
                if (await IsRefused(answer.SessionId, batch))
                    return;
            }

            await _gate.Run(() => _transport.End(answer.SessionId));
        }

        // The trace of a session that did not close goes with the start: a refused start leaves it for the next one.
        private UniTask<TelemetrySessionAnswer> StartOnTurn()
        {
            if (_isOver)
                return UniTask.FromResult(TelemetrySessionAnswer.Refused(TelemetrySendOutcome.Failed));

            _previous = _trace.ReadPrevious();

            return _transport.Start(new TelemetrySessionStart(_tags, _device.Read(), _startup.Id, _previous));
        }

        // Replaces the trace of the previous session, which the server has now.
        private void OpenTrace(Guid sessionId)
        {
            _sessionId = sessionId;
            _trace.Open(sessionId, _tags);

            if (_previous != null)
                _collection.AddMark(PreviousMark);
        }

        private UniTask WaitForNextBatch()
        {
            return UniTask.Delay(_options.SendInterval, DelayType.Realtime, PlayerLoopTiming.Update, _running.Token)
                          .SuppressCancellationThrow();
        }

        private async UniTask<bool> IsRefused(Guid sessionId, TelemetryBatch batch)
        {
            if (await _gate.Run(() => _transport.Send(sessionId, batch)) == TelemetrySendOutcome.Accepted)
                return false;

            Stop();
            return true;
        }
    }
}
