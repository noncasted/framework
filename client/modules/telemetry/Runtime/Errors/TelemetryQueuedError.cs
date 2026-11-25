namespace Global.Telemetry
{
    // An error as it came, not scrubbed: lives only until the next pass of the pump.
    public struct TelemetryQueuedError
    {
        public TelemetryQueuedError(TelemetryErrorLevel level, string category, string message, string stack)
        {
            Level = level;
            Category = category;
            Message = message;
            Stack = stack;
            Repeat = 1;
        }

        public TelemetryErrorLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public string Stack { get; }
        public int Repeat { get; private set; }

        public bool IsSame(TelemetryErrorLevel level, string category, string message, string stack)
        {
            return Level == level && Category == category && Message == message && Stack == stack;
        }

        public void CountOnce()
        {
            Repeat++;
        }
    }
}
