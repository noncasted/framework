using System.Diagnostics;

namespace Global.MatchMetrics
{
    public class MatchMetricsTicks : IMatchMetricsTicks
    {
        private static readonly double _msPerTick = 1000d / Stopwatch.Frequency;

        public double NowMs => Stopwatch.GetTimestamp() * _msPerTick;
    }
}
