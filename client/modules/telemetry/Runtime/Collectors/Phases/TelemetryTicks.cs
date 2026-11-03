using System.Diagnostics;

namespace Global.Telemetry
{
    public class TelemetryTicks : ITelemetryTicks
    {
        private static readonly double _msPerTick = 1000d / Stopwatch.Frequency;

        public double NowMs => Stopwatch.GetTimestamp() * _msPerTick;
    }
}
