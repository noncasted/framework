namespace Global.Telemetry
{
    public interface ITelemetryClock
    {
        long ElapsedMilliseconds { get; }

        void Restart();
    }
}
