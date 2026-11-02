namespace Internal
{
    // The time the latency measurements run on.
    public interface ILatencyClock
    {
        // Seconds, not stopped or scaled with the game; moves inside a frame.
        double Now { get; }

        // Seconds since the previous frame, not scaled with the game.
        float FrameDelta { get; }
    }
}
