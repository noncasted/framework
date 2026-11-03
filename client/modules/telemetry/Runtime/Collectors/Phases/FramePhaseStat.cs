namespace Global.Telemetry
{
    // The average and the largest value per frame since the last take; zero is a value.
    public class FramePhaseStat
    {
        private double _total;
        private double _max;
        private int _frames;

        public void Add(double value)
        {
            _total += value;
            _frames++;

            if (value > _max)
                _max = value;
        }

        public bool TryTake(out double average, out double max)
        {
            average = _frames == 0 ? 0d : _total / _frames;
            max = _max;
            var hasFrames = _frames > 0;
            Clear();

            return hasFrames;
        }

        public void Clear()
        {
            _total = 0d;
            _max = 0d;
            _frames = 0;
        }
    }
}
