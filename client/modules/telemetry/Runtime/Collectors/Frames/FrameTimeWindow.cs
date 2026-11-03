using System;

namespace Global.Telemetry
{
    // The frames since the last sample. A frame only writes into a preallocated array, the sort for the percentiles runs
    // once per sample, without garbage too. Over the capacity (above 4096 frames a sample) the percentiles come from the first frames kept,
    // the counters and the average stay exact.
    public class FrameTimeWindow
    {
        public const int Capacity = 4096;

        private const float _over33Seconds = 0.033f;
        private const float _over50Seconds = 0.05f;
        private const double _millisecondsInSecond = 1000d;

        private readonly float[] _deltas = new float[Capacity];

        private int _frames;
        private double _total;
        private float _max;
        private int _over33;
        private int _over50;

        // The first frame of the player has a delta of zero: it is not a frame of infinite speed.
        public void Add(float seconds)
        {
            if (seconds <= 0f)
                return;

            if (_frames < Capacity)
                _deltas[_frames] = seconds;

            _frames++;
            _total += seconds;

            if (seconds > _max)
                _max = seconds;

            if (seconds > _over33Seconds)
                _over33++;

            if (seconds > _over50Seconds)
                _over50++;
        }

        public bool TryTake(out FrameTimeStats stats)
        {
            if (_frames == 0)
            {
                stats = default;
                return false;
            }

            var kept = Math.Min(_frames, Capacity);
            Sort(kept);

            stats = new FrameTimeStats(
                _frames,
                _total / _frames * _millisecondsInSecond,
                Percentile(kept, 0.5d),
                Percentile(kept, 0.95d),
                _max * _millisecondsInSecond,
                _frames / _total,
                _over33,
                _over50);

            Clear();

            return true;
        }

        public void Clear()
        {
            _frames = 0;
            _total = 0d;
            _max = 0f;
            _over33 = 0;
            _over50 = 0;
        }

        // By insertion: Array.Sort allocates on every call, and a window holds about a hundred frames.
        private void Sort(int count)
        {
            for (var i = 1; i < count; i++)
            {
                var delta = _deltas[i];
                var place = i;

                while (place > 0 && _deltas[place - 1] > delta)
                {
                    _deltas[place] = _deltas[place - 1];
                    place--;
                }

                _deltas[place] = delta;
            }
        }

        // Nearest rank: always the delta of a real frame.
        private double Percentile(int kept, double fraction)
        {
            var rank = (int)Math.Ceiling(fraction * kept);
            return _deltas[rank - 1] * _millisecondsInSecond;
        }
    }
}
