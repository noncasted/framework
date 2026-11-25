namespace Global.Telemetry.Tests
{
    public class ManualWallClock : ITelemetryWallClock
    {
        public long NowMilliseconds { get; private set; }

        public void Set(long milliseconds)
        {
            NowMilliseconds = milliseconds;
        }
    }
}
