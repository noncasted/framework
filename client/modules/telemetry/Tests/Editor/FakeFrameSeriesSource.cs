using System.Collections.Generic;
using Internal;

namespace Global.Telemetry.Tests
{
    // Recorders of the profiler with the frames of the test: a counter exists when it was given values.
    public class FakeFrameSeriesSource : ITelemetryFrameSeriesSource
    {
        private readonly Dictionary<string, FakeFrameSeries> _series = new();

        public List<string> OpenedMarkers { get; } = new();

        public FakeFrameSeries Counter(string category, string name)
        {
            return Of(category + "/" + name);
        }

        public FakeFrameSeries Marker(string name)
        {
            return Of("marker/" + name);
        }

        public bool TryOpenCounter(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryFrameSeries series)
        {
            var found = _series.TryGetValue(category + "/" + name, out var fake);
            series = fake;
            return found;
        }

        public ITelemetryFrameSeries OpenMarker(IReadOnlyLifetime lifetime, string name)
        {
            OpenedMarkers.Add(name);
            return Of("marker/" + name);
        }

        private FakeFrameSeries Of(string key)
        {
            if (_series.TryGetValue(key, out var series) == false)
                _series.Add(key, series = new FakeFrameSeries());

            return series;
        }
    }

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
