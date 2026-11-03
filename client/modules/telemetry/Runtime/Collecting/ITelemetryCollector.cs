namespace Global.Telemetry
{
    // Called once per sample, only while a session is open. A game registers any number of them next to AddClientTelemetry.
    public interface ITelemetryCollector
    {
        void Collect(ITelemetrySample sample);
    }
}
