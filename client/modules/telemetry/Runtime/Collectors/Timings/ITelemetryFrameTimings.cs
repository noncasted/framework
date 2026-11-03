namespace Global.Telemetry
{
    public interface ITelemetryFrameTimings
    {
        // Once per frame. All zeros when the manager has nothing.
        TelemetryFrameTiming Read();
    }
}
