namespace Global.Telemetry
{
    // A collector that needs every frame of FramePhaseClock. Called inside the player loop: no allocations, no throws.
    public interface IFramePhaseListener
    {
        // Before the first measured phase.
        void OnFrameStarted();

        // After the last one. The frame is reused by the next one.
        void OnFrameEnded(FramePhaseFrame frame);
    }
}
