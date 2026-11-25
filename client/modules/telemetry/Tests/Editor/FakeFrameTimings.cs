namespace Global.Telemetry.Tests
{
    public class FakeFrameTimings : ITelemetryFrameTimings
    {
        private TelemetryFrameTiming _timing;

        public void Set(TelemetryFrameTiming timing)
        {
            _timing = timing;
        }

        public void Clear()
        {
            _timing = default;
        }

        public TelemetryFrameTiming Read()
        {
            return _timing;
        }
    }
}
