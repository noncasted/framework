namespace Global.Telemetry
{
    // One text that outlives the page: the trace of the open session. A store that cannot keep it forgets it without a word: the
    // trace is a hint for the next start, never a reason to stop the game.
    public interface ITelemetryTraceStore
    {
        // Null: nothing kept.
        string Read();
        void Write(string text);
        void Clear();
    }
}
