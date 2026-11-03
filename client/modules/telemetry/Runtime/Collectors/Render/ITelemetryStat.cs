namespace Global.Telemetry
{
    public interface ITelemetryStat
    {
        // The value of the last finished frame.
        long LastValue { get; }
    }
}
