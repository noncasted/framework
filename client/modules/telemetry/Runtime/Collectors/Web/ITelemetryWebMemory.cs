namespace Global.Telemetry
{
    // Bytes, read from the page. Below zero is not known: only Chromium has performance.memory.
    public interface ITelemetryWebMemory
    {
        double Heap { get; }
        double JsUsed { get; }
        double JsTotal { get; }
        double JsLimit { get; }
    }
}
