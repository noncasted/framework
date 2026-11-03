namespace Global.Telemetry.Tests
{
    // The page before the build in the hands of a test: none until a test gives one.
    public class FakeTelemetryPageTimeline : ITelemetryPageTimeline
    {
        private TelemetryPageTimeline _page;

        public void Set(TelemetryPageTimeline page)
        {
            _page = page;
        }

        public TelemetryPageTimeline Read()
        {
            return _page;
        }
    }
}
