namespace Global.Telemetry
{
    public class TelemetryMarkRecord
    {
        public long Time { get; private set; }
        public string Name { get; private set; }

        // A key of the code next to the name (a loaded group), null for none.
        public string Detail { get; private set; }

        // What the mark closes (a load), null for none.
        public long? DurationMs { get; private set; }

        public void Set(long time, string name, string detail, long? durationMs)
        {
            Time = time;
            Name = name;
            Detail = detail;
            DurationMs = durationMs;
        }
    }
}
