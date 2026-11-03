using System;
using System.Collections.Generic;

namespace Global.Telemetry.Tests
{
    // The page side of TelemetryGlCalls: the counters and the slow calls wait until taken.
    public class FakeTelemetryGlCalls : ITelemetryGlCalls
    {
        private readonly double[] _counters = new double[TelemetryGlCallsLayout.FirstSlow];
        private readonly List<(int Kind, double Ms)> _slow = new();

        public void Add(int slot, double value)
        {
            _counters[slot] += value;
        }

        public void Slow(int kind, double ms)
        {
            _slow.Add((kind, ms));
        }

        public int Take(double[] into)
        {
            Array.Copy(_counters, into, _counters.Length);
            Array.Clear(_counters, 0, _counters.Length);

            var count = Math.Min(_slow.Count, TelemetryGlCallsLayout.MaxSlowPerTake);

            for (var i = 0; i < count; i++)
            {
                into[TelemetryGlCallsLayout.FirstSlow + i * 2] = _slow[i].Kind;
                into[TelemetryGlCallsLayout.FirstSlow + i * 2 + 1] = _slow[i].Ms;
            }

            _slow.Clear();

            return count;
        }
    }
}
