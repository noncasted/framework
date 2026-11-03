using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry
{
    // The trace of the start of the game on the server: a snapshot when the game calls, the final trace when it closes, which replaces
    // the snapshot in the same record. What goes is decided in the turn of the gate, so the answer to a request ahead is known and
    // a trace that closed in the queue goes final at once.
    //
    // Only a call of the game opens a connection for the traces: before it the socket may not be authorized, and its first frame
    // belongs to the authorization. So a final trace with no such connection waits for the call, on a new connection too. A
    // connection takes one record: nothing is repeated on it after a refusal or a failure.
    public class TelemetryStartupReport : IScopeSetup
    {
        public TelemetryStartupReport(
            ClientTelemetryOptions options,
            ITelemetryStartupSource source,
            ITelemetryTransport transport,
            TelemetryRequestGate gate,
            ITelemetryDeviceSource device)
        {
            _options = options;
            _source = source;
            _transport = transport;
            _gate = gate;
            _device = device;
        }

        private readonly ClientTelemetryOptions _options;
        private readonly ITelemetryStartupSource _source;
        private readonly ITelemetryTransport _transport;
        private readonly TelemetryRequestGate _gate;
        private readonly ITelemetryDeviceSource _device;

        private object _link;
        private Guid _id;
        private bool _hasSnapshot;
        private bool _isOver;

        // The record of this client on the open connection. Guid.Empty: none.
        public Guid Id => _link != null && _link == _transport.Link ? _id : Guid.Empty;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            if (_options.IsEnabled)
                _source.ListenFinal(lifetime, OnFinal);
        }

        public void Send()
        {
            if (_options.IsEnabled)
                Sending(true).NoAwait();
        }

        private void OnFinal()
        {
            Sending(false).NoAwait();
        }

        // The top of a task nobody awaits. A transport may cancel when its socket is gone; anything else is a bug: one line in the
        // log and no more traces on this connection, the game goes on.
        private async UniTask Sending(bool isCallOfTheGame)
        {
            try
            {
                await _gate.Run(() => SendOnTurn(isCallOfTheGame));
            }
            catch (OperationCanceledException)
            {
                _isOver = true;
            }
            catch (Exception exception)
            {
                _isOver = true;
                TelemetrySelfLog.Exception(exception);
            }
        }

        private async UniTask SendOnTurn(bool isCallOfTheGame)
        {
            var link = _transport.Link;

            if (link == null)
            {
                Refuse(isCallOfTheGame, "no open connection");
                return;
            }

            if (link != _link)
            {
                if (isCallOfTheGame == false)
                    return;

                Begin(link);
            }

            if (_isOver)
            {
                Refuse(isCallOfTheGame, "the traces of this connection are over");
                return;
            }

            var final = _source.ReadFinal();

            if (final == null && _hasSnapshot)
                return;

            var trace = final ?? _source.ReadSnapshot();

            if (trace == null)
            {
                Refuse(isCallOfTheGame, "no trace began");
                return;
            }

            var isFinal = final != null;
            var message = await TelemetryStartupTraces.Build(_id, _device.Read(), trace, isFinal, _options.StartupFrameBudgetMs);

            Accept(await _transport.SendStartup(message), isFinal);
        }

        private void Begin(object link)
        {
            _link = link;
            _id = Guid.Empty;
            _hasSnapshot = false;
            _isOver = false;
        }

        private void Accept(TelemetryStartupAnswer answer, bool isFinal)
        {
            if (answer.Outcome != TelemetrySendOutcome.Accepted)
            {
                _isOver = true;
                return;
            }

            _id = answer.StartupId;
            _hasSnapshot = true;
            _isOver = isFinal;
        }

        private static void Refuse(bool isCallOfTheGame, string reason)
        {
            if (isCallOfTheGame)
                TelemetrySelfLog.Line($"The startup trace is not sent: {reason}");
        }
    }
}
