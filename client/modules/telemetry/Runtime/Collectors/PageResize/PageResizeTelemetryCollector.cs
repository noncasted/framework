using Internal;

namespace Global.Telemetry
{
    // A new size of the canvas as the mark page.resize with both sizes in the detail: a black screen after a fullscreen or a rotation
    // shows next to it whether the buffer followed the canvas. The page reports a size once it holds.
    public class PageResizeTelemetryCollector : ITelemetryLiveCollector
    {
        public PageResizeTelemetryCollector(ITelemetryPage page, TelemetryBuffer buffer, ITelemetryClock clock)
        {
            _page = page;
            _buffer = buffer;
            _clock = clock;
        }

        public const int MaxMarks = 32;

        private const string Mark = "page.resize";

        private readonly ITelemetryPage _page;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private int _marks;

        public void Start(IReadOnlyLifetime running)
        {
            _marks = 0;
            _page.Resized.Advise(running, OnResized);
        }

        private void OnResized(TelemetryCanvasSize size)
        {
            if (_marks == MaxMarks)
                return;

            _marks++;
            _buffer.AddMark(_clock.ElapsedMilliseconds, Mark, size.ToString());
        }
    }
}
