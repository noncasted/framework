namespace Global.Telemetry
{
    // Outside the web the build is the first code of the start.
    public class NoTelemetryPageTimeline : ITelemetryPageTimeline
    {
        public TelemetryPageTimeline Read()
        {
            return null;
        }
    }
}
