namespace Global.Telemetry.Tests
{
    public class FixedDeviceSource : ITelemetryDeviceSource
    {
        public FixedDeviceSource(TelemetryDevice device)
        {
            _device = device;
        }

        private readonly TelemetryDevice _device;

        public TelemetryDevice Read()
        {
            return _device;
        }
    }
}
