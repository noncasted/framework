namespace Global.Telemetry
{
    public interface ITelemetrySample
    {
        // A number under a name from the code (see TelemetryNameGuard); a new name may appear at any moment. The unit
        // belongs to the name: it is sent once with it, the same name with another unit is a mistake of the code.
        // A non-finite value counts as no value in this sample.
        void Gauge(string name, double value, string unit = null);
    }
}
