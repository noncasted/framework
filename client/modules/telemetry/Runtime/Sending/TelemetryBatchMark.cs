namespace Global.Telemetry
{
    public readonly struct TelemetryBatchMark
    {
        public TelemetryBatchMark(long time, string name)
        {
            Time = time;
            Name = name;
        }

        public long Time { get; }
        public string Name { get; }
    }
}
