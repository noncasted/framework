namespace Global.Telemetry
{
    // The clock of the device, only for the time between two records of this device: its value never leaves it.
    public interface ITelemetryWallClock
    {
        long NowMilliseconds { get; }
    }
}
