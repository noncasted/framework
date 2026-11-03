namespace Global.Telemetry
{
    public enum TelemetrySendOutcome
    {
        // Taken, the flag is on.
        Accepted,

        // The flag is off, or the server closed this session: nothing more until the next session, which asks again.
        Disabled,

        // No answer: nothing more until the next session, nothing is repeated.
        Failed
    }
}
