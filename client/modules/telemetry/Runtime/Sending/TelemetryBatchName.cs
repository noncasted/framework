namespace Global.Telemetry
{
    public readonly struct TelemetryBatchName
    {
        public TelemetryBatchName(string name, string unit)
        {
            Name = name;
            Unit = unit;
        }

        public string Name { get; }

        // Null: no unit.
        public string Unit { get; }
    }
}
