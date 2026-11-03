namespace Global.Telemetry.Tests.Transport
{
    public class TelemetryFixedDevice : ITelemetryDeviceSource
    {
        public TelemetryFixedDevice(TelemetryDevice device)
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
