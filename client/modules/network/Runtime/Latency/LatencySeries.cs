namespace Internal
{
    // The last samples of one measured value, in milliseconds.
    public class LatencySeries
    {
        public LatencySeries(int capacity)
        {
            _samples = new float[capacity];
        }

        private readonly float[] _samples;

        private int _next;
        private int _count;

        public int Count => _count;
        public float Last { get; private set; }

        public float Average
        {
            get
            {
                var sum = 0f;

                for (var i = 0; i < _count; i++)
                    sum += _samples[i];

                return sum / _count;
            }
        }

        public float Max
        {
            get
            {
                var max = 0f;

                for (var i = 0; i < _count; i++)
                {
                    if (_samples[i] > max)
                        max = _samples[i];
                }

                return max;
            }
        }

        public void Add(float value)
        {
            Last = value;
            _samples[_next] = value;
            _next = (_next + 1) % _samples.Length;

            if (_count < _samples.Length)
                _count++;
        }

        public string Format()
        {
            if (_count == 0)
                return "-";

            return $"{Last:0} (avg {Average:0}, max {Max:0})";
        }
    }
}
