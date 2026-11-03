using System.Diagnostics;

namespace Global.Telemetry
{
    // The time of a session is counted from its start: the clock of the device is not sent.
    public class TelemetryClock : ITelemetryClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        public void Restart()
        {
            _stopwatch.Restart();
        }
    }
}
