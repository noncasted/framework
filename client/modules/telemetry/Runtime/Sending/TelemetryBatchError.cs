namespace Global.Telemetry
{
    public readonly struct TelemetryBatchError
    {
        public TelemetryBatchError(long time, TelemetryErrorLevel level, string message, string stack, int repeat)
        {
            Time = time;
            Level = level;
            Message = message;
            Stack = stack;
            Repeat = repeat;
        }

        public long Time { get; }
        public TelemetryErrorLevel Level { get; }

        // Free of personal data: scrubbed on the way to the buffer.
        public string Message { get; }

        // Null: no stack.
        public string Stack { get; }

        public int Repeat { get; }
    }
}
