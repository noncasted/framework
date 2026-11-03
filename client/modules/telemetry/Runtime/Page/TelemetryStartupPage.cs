namespace Global.Telemetry
{
    // The page before the build, placed ahead of the startup trace. The two clocks meet at the reading: the page tells its time now,
    // the trace how long ago it began, so the start of the build is the difference. Read at every sending: a file or a span of the
    // template may end after the start of the build.
    public class TelemetryStartupPage
    {
        public TelemetryStartupPage(ITelemetryPageTimeline timeline, ITelemetryStartupSource source)
        {
            _timeline = timeline;
            _source = source;
        }

        private readonly ITelemetryPageTimeline _timeline;
        private readonly ITelemetryStartupSource _source;

        public TelemetryStartupPrefix Read()
        {
            var page = _timeline.Read();

            if (page == null)
                return TelemetryStartupPrefix.None;

            var buildStartMs = page.now - _source.SinceStartMs();

            if (buildStartMs <= 0d)
                return TelemetryStartupPrefix.None;

            return new TelemetryStartupPrefix(TelemetryPageSpans.Build(page, buildStartMs), buildStartMs);
        }
    }
}
