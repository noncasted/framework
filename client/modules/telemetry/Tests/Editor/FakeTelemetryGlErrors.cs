using System;
using System.Collections.Generic;

namespace Global.Telemetry.Tests
{
    // The page side of TelemetryGlErrors: the codes wait until taken.
    public class FakeTelemetryGlErrors : ITelemetryGlErrors
    {
        private readonly List<int> _codes = new List<int>();

        public int LastMax { get; private set; }
        public double LastTakeMs { get; private set; }

        public void CostOfTheNextTake(double ms)
        {
            LastTakeMs = ms;
        }

        public void Raise(params int[] codes)
        {
            _codes.AddRange(codes);
        }

        public int Take(int[] into)
        {
            LastMax = into.Length;

            var count = Math.Min(into.Length, _codes.Count);
            _codes.CopyTo(0, into, 0, count);
            _codes.RemoveRange(0, count);

            return count;
        }
    }
}
