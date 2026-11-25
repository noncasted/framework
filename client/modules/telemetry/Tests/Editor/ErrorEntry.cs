namespace Global.Telemetry.Tests
{
    public class ErrorEntry
    {
        public ErrorEntry(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            Level = level;
            Category = category;
            Message = message;
            Stack = stack;
            Repeat = repeat;
        }

        public TelemetryErrorLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public string Stack { get; }
        public int Repeat { get; }
    }
}
