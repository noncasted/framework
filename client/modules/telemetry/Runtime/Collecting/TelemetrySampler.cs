using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    // One pass over the collectors gives one sample in the buffer. Reuses its array: no garbage per sample.
    public class TelemetrySampler : ITelemetrySample
    {
        public TelemetrySampler(
            IReadOnlyList<ITelemetryCollector> collectors,
            TelemetryNameTable names,
            TelemetryBuffer buffer,
            ITelemetryClock clock,
            TelemetryVisibility visibility)
        {
            _collectors = collectors;
            _names = names;
            _buffer = buffer;
            _clock = clock;
            _visibility = visibility;
            Array.Fill(_values, double.NaN);
        }

        private readonly IReadOnlyList<ITelemetryCollector> _collectors;
        private readonly TelemetryNameTable _names;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly TelemetryVisibility _visibility;
        private readonly double[] _values = new double[TelemetryLimits.MaxNamesPerSession];
        private bool _hasValues;

        public void Sample()
        {
            var time = _clock.ElapsedMilliseconds;
            var isHidden = _visibility.TakeWasHidden();

            for (var i = 0; i < _collectors.Count; i++)
            {
                // The isolation of subscribers: a collector of the game that throws does not take the others down. It is
                // an error of the telemetry for the log of the game, not a record: it would come every sample.
                try
                {
                    _collectors[i].Collect(this);
                }
                catch (Exception exception)
                {
                    TelemetrySelfLog.Exception(exception);
                }
            }

            if (_hasValues == false)
                return;

            _buffer.AddSample(time, _values, _names.Count, isHidden);
            Array.Fill(_values, double.NaN, 0, _names.Count);
            _hasValues = false;
        }

        public void Gauge(string name, double value, string unit = null)
        {
            if (_names.TryGetIndex(name, unit, out var index) == false)
                return;

            _values[index] = value;
            _hasValues = true;
        }
    }
}
