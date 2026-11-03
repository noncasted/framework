namespace Global.Telemetry.Tests
{
    public class ManualTicks : ITelemetryTicks
    {
        public double NowMs { get; private set; } = 1000d;

        public void Advance(double milliseconds)
        {
            NowMs += milliseconds;
        }
    }
}
