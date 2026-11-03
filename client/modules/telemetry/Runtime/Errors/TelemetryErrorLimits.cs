namespace Global.Telemetry
{
    // A stream of errors must cost the game next to nothing, so the way of an error is bounded at every step.
    public static class TelemetryErrorLimits
    {
        // Between two passes of the pump. Errors over it are lost.
        public const int MaxQueued = 128;

        // New texts through the regular expressions in one pass, a message and a stack count as two. A known text costs
        // nothing, so this only cuts a message with a new id each time: such a stream counts less than it happened.
        public const int MaxScrubsPerPass = 8;

        // The known texts, by kind of text. When full the memo starts over: the price is one more scrubbing.
        public const int MaxMemoTexts = 32;

        // Different errors of a session. A new one over it is lost, the known ones go on counting.
        public const int MaxGroups = 64;

        public const int MaxRecordsPerSession = 256;

        // One window is one pass of the pump, about a second. The rest of the due records wait for the next one: a batch
        // takes about two records of the full size.
        public const int MaxRecordsPerWindow = 4;

        // The first record of an error goes soon, it may be the last moment of the tab; each next one waits twice as long,
        // so one error in a loop for an hour takes a few dozen records.
        public const long FirstWindowMs = 1000;
        public const long MaxWindowMs = 60_000;

        // Warnings take a small part of every limit, so a game that warns all the time neither pays for it nor pushes the errors out:
        // in the queue between two passes, kinds in a session and records in a session. A new one over a limit is lost.
        public const int MaxQueuedWarnings = 16;
        public const int MaxWarningGroups = 16;
        public const int MaxWarningRecordsPerSession = 48;

        // The two errors with one start of the message and one stack are the same error.
        public const int KeyMessageLength = 128;
    }
}
