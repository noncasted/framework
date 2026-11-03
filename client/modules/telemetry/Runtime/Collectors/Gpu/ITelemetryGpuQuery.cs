namespace Global.Telemetry
{
    // A timer query of the GPU on the context of the game (EXT_disjoint_timer_query_webgl2). One at a time; the result comes
    // frames later. Nothing here throws or breaks the frame: without the extension, on a lost context, while another query of
    // the same kind is open, Begin returns false.
    public interface ITelemetryGpuQuery
    {
        bool IsAvailable { get; }

        bool Begin();
        void End();

        // Milliseconds of the GPU of the last ended query; below zero: not ready, lost or disjoint (the timer was not valid).
        double TakeMs();

        // A query of a session that ended is dropped.
        void Drop();
    }
}
