namespace Global.Telemetry
{
    // Read for the start of a session and for a startup trace.
    public interface ITelemetryDeviceSource
    {
        TelemetryDevice Read();
    }
}
