namespace Global.Telemetry
{
    public class TelemetryMarkRecord
    {
        public long Time { get; private set; }
        public string Name { get; private set; }

        public void Set(long time, string name)
        {
            Time = time;
            Name = name;
        }
    }
}
