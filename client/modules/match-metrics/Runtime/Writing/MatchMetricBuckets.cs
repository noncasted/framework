using System;
using System.Collections.Generic;
using Shared;

namespace Global.MatchMetrics
{
    // The buckets of one stream: a second of the clock of the run each, the same for every series. The current bucket of
    // every slot takes the writes; when the clock passes into the next second the values move into the rows of the open
    // chunk. The rows are arrays by slot that grow only, so a write allocates nothing.
    //
    // The chunk is one run of buckets whose real time goes with the clock: where they part by more than MaxDrift seconds
    // (a pause stops the clock, a guest jumps to the clock of the host) the open chunk ends and the next one begins at the
    // current bucket with the real time of now. Cut takes the closed buckets for a batch; the next chunk goes on from
    // the current bucket on the same real time.
    public class MatchMetricBuckets
    {
        public MatchMetricBuckets(IMatchMetricsClock clock, IMatchMetricsTicks ticks)
        {
            _clock = clock;
            _ticks = ticks;
        }

        public const double MaxDrift = 1d;

        private const int InitialRows = 64;

        private readonly IMatchMetricsClock _clock;
        private readonly IMatchMetricsTicks _ticks;
        private readonly List<SharedMatchMetrics.MatchSeriesKind> _kinds = new();
        private readonly List<MatchChunkRecord> _finished = new();

        private float[] _current = new float[16];
        private float[][] _rows = new float[16][];
        private int _rowCapacity = InitialRows;

        // The current bucket, -1 before the first write or sync.
        private int _bucket = -1;
        private int _chunkStart;
        private int _closed;
        private double _chunkStartMs;

        public int Count => _kinds.Count;

        public int Declare(SharedMatchMetrics.MatchSeriesKind kind)
        {
            var slot = _kinds.Count;

            if (slot == _current.Length)
            {
                Array.Resize(ref _current, slot * 2);
                Array.Resize(ref _rows, slot * 2);
            }

            _kinds.Add(kind);
            _current[slot] = EmptyOf(kind);

            var row = new float[_rowCapacity];
            Array.Fill(row, EmptyOf(kind), 0, _closed);
            _rows[slot] = row;

            return slot;
        }

        public void Add(int slot, float value)
        {
            SyncOnWrite();
            _current[slot] += value;
        }

        public void Set(int slot, float value)
        {
            SyncOnWrite();
            _current[slot] = value;
        }

        // Moves the buckets on to the clock and checks the real time against it. Called on every write into another second
        // and once a while by the sender: a stopped clock is found by the real time only.
        public void Sync()
        {
            var clock = _clock.Seconds;
            var nowMs = _ticks.NowMs;
            var bucket = Math.Max((int)Math.Floor(clock), _bucket);

            if (_bucket < 0)
            {
                Begin(bucket, clock, nowMs);
                return;
            }

            var drift = (nowMs - _chunkStartMs) / 1000d - (clock - _chunkStart);

            if (Math.Abs(drift) > MaxDrift)
            {
                // A jump: the bucket before it ends the chunk. A stop: the current bucket goes on in the next chunk.
                if (bucket > _bucket)
                    CloseBucket();

                Finish();
                Begin(bucket, clock, nowMs);
                return;
            }

            while (_bucket < bucket)
                CloseBucket();
        }

        // The chunks of the closed buckets, the series with nothing in them left out. The last cut closes the current bucket too.
        public List<MatchChunkRecord> Cut(bool isLast)
        {
            Sync();

            if (isLast)
                CloseBucket();

            Finish();

            var chunks = new List<MatchChunkRecord>(_finished);
            _finished.Clear();

            return chunks;
        }

        private void SyncOnWrite()
        {
            if (_bucket < 0 || (int)Math.Floor(_clock.Seconds) > _bucket)
                Sync();
        }

        private void Begin(int bucket, float clock, double nowMs)
        {
            _bucket = bucket;
            _chunkStart = bucket;
            _chunkStartMs = nowMs - (clock - bucket) * 1000d;
            _closed = 0;
        }

        private void CloseBucket()
        {
            if (_closed == _rowCapacity)
                GrowRows();

            for (var slot = 0; slot < _kinds.Count; slot++)
            {
                _rows[slot][_closed] = _current[slot];
                _current[slot] = EmptyOf(_kinds[slot]);
            }

            _closed++;
            _bucket++;
        }

        // The closed buckets of the open chunk become records; the chunk goes on from the current bucket on the same real time.
        private void Finish()
        {
            for (var slot = 0; slot < _kinds.Count; slot++)
                AddRecord(slot);

            _chunkStartMs += _closed * 1000d;
            _chunkStart += _closed;
            _closed = 0;
        }

        // Without the empty buckets at both ends; a series empty in the whole chunk gives nothing.
        private void AddRecord(int slot)
        {
            var row = _rows[slot];
            var empty = EmptyOf(_kinds[slot]);
            var first = 0;
            var last = _closed - 1;

            while (first <= last && IsEmpty(row[first], empty))
                first++;

            while (last >= first && IsEmpty(row[last], empty))
                last--;

            if (first > last)
                return;

            var values = new float[last - first + 1];
            Array.Copy(row, first, values, 0, values.Length);
            _finished.Add(new MatchChunkRecord(slot, _chunkStart + first, _chunkStartMs + first * 1000d, values));
        }

        private void GrowRows()
        {
            _rowCapacity *= 2;

            for (var slot = 0; slot < _kinds.Count; slot++)
                Array.Resize(ref _rows[slot], _rowCapacity);
        }

        private static float EmptyOf(SharedMatchMetrics.MatchSeriesKind kind)
        {
            return kind == SharedMatchMetrics.MatchSeriesKind.Counter ? 0f : float.NaN;
        }

        private static bool IsEmpty(float value, float empty)
        {
            return float.IsNaN(empty) ? float.IsNaN(value) : value == empty;
        }
    }
}
