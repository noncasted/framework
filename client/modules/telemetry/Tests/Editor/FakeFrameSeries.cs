using System.Collections.Generic;

namespace Global.Telemetry.Tests
{
    public class FakeFrameSeries : ITelemetryFrameSeries
    {
        private readonly List<double> _frames = new();

        public void Frames(params double[] values)
        {
            _frames.AddRange(values);
        }

        public int Take(out double sum, out double max)
        {
            sum = 0d;
            max = 0d;

            foreach (var value in _frames)
            {
                sum += value;
                max = value > max ? value : max;
            }

            var count = _frames.Count;
            _frames.Clear();
            return count;
        }
    }
}
