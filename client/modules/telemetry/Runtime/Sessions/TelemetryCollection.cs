using System;
using System.Collections.Generic;
using Internal;

namespace Global.Telemetry
{
    // What collects for the one session open right now. It starts clean for each: the buffer, the names, the clock of the
    // session and the live collectors, so nothing of a session gets into the next one.
    public class TelemetryCollection
    {
        public TelemetryCollection(
            TelemetryBuffer buffer,
            TelemetryNameTable names,
            TelemetrySampler sampler,
            TelemetryBatchWriter writer,
            ITelemetryClock clock,
            TelemetryVisibility visibility,
            IReadOnlyList<ITelemetryLiveCollector> liveCollectors)
        {
            _buffer = buffer;
            _names = names;
            _sampler = sampler;
            _writer = writer;
            _clock = clock;
            _visibility = visibility;
            _liveCollectors = liveCollectors;
        }

        private readonly TelemetryBuffer _buffer;
        private readonly TelemetryNameTable _names;
        private readonly TelemetrySampler _sampler;
        private readonly TelemetryBatchWriter _writer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryVisibility _visibility;
        private readonly IReadOnlyList<ITelemetryLiveCollector> _liveCollectors;

        public ILifetime Begin()
        {
            var running = new Lifetime();

            _names.Clear();
            _clock.Restart();
            _buffer.Open();
            _visibility.Listen(running);
            StartLiveCollectors(running);

            return running;
        }

        public void Sample()
        {
            _sampler.Sample();
        }

        // The end of the lifetime goes first: what the collectors have ready for a closing session gets into its last batches.
        public IReadOnlyList<TelemetryBatch> End(ILifetime running)
        {
            running.Terminate();

            var batches = _writer.WriteAll();
            _buffer.Close();

            return batches;
        }

        public void Drop(ILifetime running)
        {
            running.Terminate();
            _buffer.Close();
        }

        // The isolation of subscribers, as in the sampler: a collector of the game that throws does not take the others down.
        private void StartLiveCollectors(ILifetime running)
        {
            for (var i = 0; i < _liveCollectors.Count; i++)
            {
                try
                {
                    _liveCollectors[i].Start(running);
                }
                catch (Exception exception)
                {
                    TelemetrySelfLog.Exception(exception);
                }
            }
        }
    }
}
