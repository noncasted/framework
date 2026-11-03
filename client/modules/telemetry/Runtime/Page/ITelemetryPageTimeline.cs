namespace Global.Telemetry
{
    // What the page did before the build started. Null outside the web.
    public interface ITelemetryPageTimeline
    {
        TelemetryPageTimeline Read();
    }
}
