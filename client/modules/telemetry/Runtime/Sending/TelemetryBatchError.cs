namespace Global.Telemetry
{
    public readonly struct TelemetryBatchError
    {
        public TelemetryBatchError(long time, TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            Time = time;
            Level = level;
            Category = category;
            Message = message;
            Stack = stack;
            Repeat = repeat;
        }

        public long Time { get; }
        public TelemetryErrorLevel Level { get; }

        // The category of the logger, null for an error that did not come from it.
        public string Category { get; }

        // Free of personal data: scrubbed on the way to the buffer.
        public string Message { get; }

        // Null: no stack.
        public string Stack { get; }

        public int Repeat { get; }
    }
}
