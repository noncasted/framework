namespace Global.Telemetry
{
    public enum TelemetryErrorLevel
    {
        Error,
        Exception,

        // A warning of the log of Unity: its own limits (TelemetryErrorLimits), counted apart by the server.
        Warning
    }
}
