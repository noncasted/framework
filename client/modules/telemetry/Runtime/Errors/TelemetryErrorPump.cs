using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry
{
    // The main thread side of the errors: the queue goes through the scrubbing into the groups, the groups give records.
    // Started for every session with its limits fresh; the end of the session takes what is pending into its last batches
    // and closes the queue. Everything that may break or log here runs under the guard of TelemetrySelfLog.
    public class TelemetryErrorPump : ITelemetryLiveCollector
    {
        public TelemetryErrorPump(
            TelemetryErrorQueue queue,
            TelemetryTextCleaner cleaner,
            TelemetryErrorGroups groups,
            ClientTelemetryOptions options)
        {
            _queue = queue;
            _cleaner = cleaner;
            _groups = groups;
            _options = options;
            _pumpPass = PumpPass;
            _flushPass = FlushPass;
        }

        private readonly TelemetryErrorQueue _queue;
        private readonly TelemetryTextCleaner _cleaner;
        private readonly TelemetryErrorGroups _groups;
        private readonly ClientTelemetryOptions _options;
        private readonly TelemetryQueuedError[] _batch = new TelemetryQueuedError[TelemetryErrorLimits.MaxQueued];
        private readonly Action _pumpPass;
        private readonly Action _flushPass;

        public void Start(IReadOnlyLifetime running)
        {
            _queue.Open();
            _groups.Reset();
            running.Listen(End);
            Pumping(running).NoAwait();
        }

        public void Pump()
        {
            TelemetrySelfLog.Guard(_pumpPass);
        }

        public void FlushAll()
        {
            TelemetrySelfLog.Guard(_flushPass);
        }

        private void End()
        {
            FlushAll();
            _queue.Close();
        }

        // Ends with the lifetime: NoAwait lets the cancellation go.
        private async UniTask Pumping(IReadOnlyLifetime running)
        {
            while (_queue.IsClosed == false)
            {
                await UniTask.Delay(_options.SampleInterval, DelayType.Realtime, PlayerLoopTiming.Update, running.Token);
                Pump();
            }
        }

        private void PumpPass()
        {
            TakeQueued();
            _groups.FlushDue();
            CloseWhenSpent();
        }

        private void FlushPass()
        {
            TakeQueued();
            _groups.FlushAll();
            CloseWhenSpent();
        }

        private void TakeQueued()
        {
            var count = _queue.Drain(_batch);

            _cleaner.BeginPass();

            for (var i = 0; i < count; i++)
                Take(_batch[i]);

            Array.Clear(_batch, 0, count);
        }

        private void Take(in TelemetryQueuedError error)
        {
            if (_cleaner.TryCleanMessage(error.Message, out var message) == false)
                return;

            if (_cleaner.TryCleanStack(error.Stack, out var stack) == false)
                return;

            _groups.Count(error.Level, error.Category, message, stack, error.Repeat);
        }

        private void CloseWhenSpent()
        {
            if (_groups.IsSpent == false)
                return;

            _queue.Close();
            _groups.Clear();
        }
    }
}
