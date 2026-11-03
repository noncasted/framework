namespace Global.Telemetry
{
    // The error flags of the WebGL context of the game. Nothing asks for them in a release build, so each code stays set until taken.
    public interface ITelemetryGlErrors
    {
        // Up to the length of the array, the codes in the order the context gives them. The count taken.
        int Take(int[] into);

        // How long the last take kept the main thread: each question waits for the context to finish what it was given.
        double LastTakeMs { get; }
    }
}
