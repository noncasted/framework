namespace Global.Telemetry
{
    // The interval of requestAnimationFrame of the page, measured in the browser apart from the frames of Unity.
    public interface ITelemetryRaf
    {
        void Start();
        void Stop();

        // Milliseconds since the previous take; below zero when no interval came.
        double TakeAverageMs();
        double TakeMaxMs();
    }
}
