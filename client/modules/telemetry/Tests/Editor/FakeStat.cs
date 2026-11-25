namespace Global.Telemetry.Tests
{
    public class FakeStat : ITelemetryStat
    {
        public long LastValue { get; private set; }

        public void Set(long value)
        {
            LastValue = value;
        }
    }
}
