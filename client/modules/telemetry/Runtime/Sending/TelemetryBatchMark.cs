namespace Global.Telemetry
{
    public readonly struct TelemetryBatchMark
    {
        public TelemetryBatchMark(long time, string name, string detail = null, long? durationMs = null)
        {
            Time = time;
            Name = name;
            Detail = detail;
            DurationMs = durationMs;
        }

        public long Time { get; }
        public string Name { get; }
        public string Detail { get; }
        public long? DurationMs { get; }
    }
}
