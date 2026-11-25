namespace Global.Telemetry
{
    public class TelemetryErrorRecord
    {
        public long Time { get; private set; }
        public TelemetryErrorLevel Level { get; private set; }
        public string Category { get; private set; }
        public string Message { get; private set; }
        public string Stack { get; private set; }
        public int Repeat { get; private set; }

        public void Set(long time, TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            Time = time;
            Level = level;
            Category = category;
            Message = message;
            Stack = stack;
            Repeat = repeat;
        }
    }
}
