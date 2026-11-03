using System;

namespace Global.Telemetry
{
    public class TelemetrySampleRecord
    {
        private double[] _values = Array.Empty<double>();

        public long Time { get; private set; }
        public int Count { get; private set; }
        public bool IsHidden { get; private set; }

        public void Set(long time, double[] values, int count, bool isHidden)
        {
            if (_values.Length < count)
                _values = new double[count];

            Array.Copy(values, _values, count);
            Time = time;
            Count = count;
            IsHidden = isHidden;
        }

        public double ValueAt(int index)
        {
            return _values[index];
        }
    }
}
