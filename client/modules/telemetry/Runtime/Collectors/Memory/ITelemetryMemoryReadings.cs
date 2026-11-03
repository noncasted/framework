namespace Global.Telemetry
{
    // Bytes; a counter that does not work on this platform reads zero.
    public interface ITelemetryMemoryReadings
    {
        long Allocated { get; }
        long Reserved { get; }
        long MonoUsed { get; }
        long MonoHeap { get; }
        long Textures { get; }
        int Collections { get; }
    }
}
