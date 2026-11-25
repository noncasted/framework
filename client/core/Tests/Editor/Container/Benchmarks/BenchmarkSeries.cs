using System;
using System.Collections.Generic;

namespace Internal.Tests
{
    // Time is the median of runs, bytes are the minimum: the counter also sees other editor threads,
    // and extra bytes can only be added.
    internal sealed class BenchmarkSeries
    {
        public BenchmarkSeries(string name)
        {
            _name = name;
        }

        private readonly string _name;
        private readonly List<BenchmarkRun> _runs = new List<BenchmarkRun>();

        public void Add(Action action)
        {
            _runs.Add(BenchmarkMeasure.Capture(action));
        }

        public BenchmarkSample ToSample()
        {
            if (_runs.Count == 0)
                throw new InvalidOperationException(_name + " has no runs");

            var ticks = new long[_runs.Count];
            var minBytes = long.MaxValue;
            var maxBytes = long.MinValue;

            for (var i = 0; i < _runs.Count; i++)
            {
                ticks[i] = _runs[i].Ticks;
                minBytes = Math.Min(minBytes, _runs[i].AllocatedBytes);
                maxBytes = Math.Max(maxBytes, _runs[i].AllocatedBytes);
            }

            Array.Sort(ticks);

            return new BenchmarkSample(
                _name,
                ticks.Length,
                ticks[ticks.Length / 2],
                ticks[0],
                ticks[ticks.Length - 1],
                minBytes,
                maxBytes);
        }
    }
}
