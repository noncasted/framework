namespace Global.Telemetry
{
    // Only the frames that have a value: GPU timers answer late, a gap must not pull the average down.
    public class FrameTimingAverage
    {
        private double _total;
        private int _count;

        public void Add(double milliseconds)
        {
            if (milliseconds <= 0d)
                return;

            _total += milliseconds;
            _count++;
        }

        public bool TryTake(out double average)
        {
            if (_count == 0)
            {
                average = 0d;
                return false;
            }

            average = _total / _count;
            Clear();

            return true;
        }

        public void Clear()
        {
            _total = 0d;
            _count = 0;
        }
    }
}
