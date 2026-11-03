namespace Global.Telemetry
{
    // Recorded: the errors emscripten recorded itself. Checked: the calls the check found failing, zero without it.
    public readonly struct TelemetryGlCallErrorCounts
    {
        public TelemetryGlCallErrorCounts(int recorded, int @checked, bool isChecking)
        {
            Recorded = recorded;
            Checked = @checked;
            IsChecking = isChecking;
        }

        public int Recorded { get; }
        public int Checked { get; }
        public bool IsChecking { get; }
    }
}
