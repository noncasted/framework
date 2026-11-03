namespace Global.Telemetry
{
    // What ITelemetryGpuQuery.TakeMs returns besides a time.
    public static class TelemetryGpuQueryResults
    {
        public const double NotReady = -1d;
        public const double Lost = -2d;
    }
}
