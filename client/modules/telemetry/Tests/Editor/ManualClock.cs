namespace Global.Telemetry.Tests
{
    public class ManualClock : ITelemetryClock
    {
        public long ElapsedMilliseconds { get; private set; }
        public int Restarts { get; private set; }

        public void Set(long milliseconds)
        {
            ElapsedMilliseconds = milliseconds;
        }

        public void Restart()
        {
            Restarts++;
            ElapsedMilliseconds = 0;
        }
    }
}
