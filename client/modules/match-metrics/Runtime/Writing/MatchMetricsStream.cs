using System;
using System.Collections.Generic;
using Shared;

namespace Global.MatchMetrics
{
    // The writing side of a stream: the series by slots, the events and the declarations not yet in a batch. Cut takes them
    // into a batch; the last cut ends the stream.
    public class MatchMetricsStream : IMatchMetricsStream
    {
        public MatchMetricsStream(IMatchMetricsClock clock, IMatchMetricsTicks ticks)
        {
            _clock = clock;
            _ticks = ticks;
            _buckets = new MatchMetricBuckets(clock, ticks);
        }

        private readonly IMatchMetricsClock _clock;
        private readonly IMatchMetricsTicks _ticks;
        private readonly MatchMetricBuckets _buckets;
        private readonly Dictionary<string, object> _handles = new();
        private readonly List<MatchSeriesRecord> _newSeries = new();
        private readonly List<MatchEventRecord> _events = new();

        public bool IsEnded { get; private set; }

        public IMatchCounter Counter(string name, MatchTags tags = default)
        {
            return (IMatchCounter)Declare(name, tags, SharedMatchMetrics.MatchSeriesKind.Counter);
        }

        public IMatchGauge Gauge(string name, MatchTags tags = default)
        {
            return (IMatchGauge)Declare(name, tags, SharedMatchMetrics.MatchSeriesKind.Gauge);
        }

        public void Event(string name, params MatchEventField[] fields)
        {
            RequireOpen();
            MatchMetricNames.RequireName(name);

            if (fields.Length > SharedMatchMetricsLimits.MaxFields)
                throw new ArgumentException($"[MatchMetrics] The event '{name}' has {fields.Length} fields, over {SharedMatchMetricsLimits.MaxFields}");

            _events.Add(new MatchEventRecord(name, _clock.Seconds, _ticks.NowMs, (MatchEventField[])fields.Clone()));
        }

        public void Add(int slot, float value)
        {
            RequireValue(value);
            _buckets.Add(slot, value);
        }

        public void Set(int slot, float value)
        {
            RequireValue(value);
            _buckets.Set(slot, value);
        }

        // Once a while: a stopped clock is found by the real time only.
        public void Sync()
        {
            RequireOpen();
            _buckets.Sync();
        }

        // Null: nothing new. The last cut takes the current bucket too and ends the writing.
        public MatchMetricsBatchRecord Cut(bool isLast)
        {
            RequireOpen();

            var chunks = _buckets.Cut(isLast);
            IsEnded = isLast;

            if (chunks.Count == 0 && _events.Count == 0 && _newSeries.Count == 0)
                return null;

            var batch = new MatchMetricsBatchRecord(new List<MatchSeriesRecord>(_newSeries), chunks, _events.ToArray());
            _newSeries.Clear();
            _events.Clear();

            return batch;
        }

        private object Declare(string name, MatchTags tags, SharedMatchMetrics.MatchSeriesKind kind)
        {
            RequireOpen();
            MatchMetricNames.RequireName(name);

            var key = tags.ToKey(name);

            if (_handles.TryGetValue(key, out var existing))
            {
                var isSameKind = kind == SharedMatchMetrics.MatchSeriesKind.Counter ? existing is IMatchCounter : existing is IMatchGauge;

                if (isSameKind == false)
                    throw new ArgumentException($"[MatchMetrics] The series '{key}' is declared as another kind");

                return existing;
            }

            if (_buckets.Count == SharedMatchMetricsLimits.MaxSeries)
                throw new InvalidOperationException($"[MatchMetrics] Over {SharedMatchMetricsLimits.MaxSeries} series: '{key}'");

            var slot = _buckets.Declare(kind);
            object handle = kind == SharedMatchMetrics.MatchSeriesKind.Counter ? new MatchCounter(this, slot) : new MatchGauge(this, slot);

            _handles.Add(key, handle);
            _newSeries.Add(new MatchSeriesRecord(slot, name, kind, tags));

            return handle;
        }

        private void RequireValue(float value)
        {
            if (IsEnded)
                throw new InvalidOperationException("[MatchMetrics] A write into an ended stream");

            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException($"[MatchMetrics] Not a finite value: {value}");
        }

        private void RequireOpen()
        {
            if (IsEnded)
                throw new InvalidOperationException("[MatchMetrics] The stream is ended");
        }
    }
}
