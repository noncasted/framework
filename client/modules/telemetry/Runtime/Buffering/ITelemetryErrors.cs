namespace Global.Telemetry
{
    // The way of an error into the batches, TelemetryErrorPump stands on top of this. The text is the only free text of the module, so it comes here already free of personal data. Main thread only.
    public interface ITelemetryErrors
    {
        // Category: the category of the logger that wrote it (its message is the template), null for the other errors.
        void Add(TelemetryErrorLevel level, string category, string message, string stack, int repeat);
    }
}
