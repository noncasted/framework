using Internal;

namespace Global.Telemetry
{
    // The events of the life of the page as marks of the open session: whether a page that died went away (pagehide), went
    // into the cache or was frozen in the background, or died while it was shown; its fullscreen and the state of the audio of Unity. A mark also renews the trace of the session
    // (TelemetrySessionTrace), so the next start knows the last events of a page that did not close it.
    public class PageLifecycleTelemetryCollector : ITelemetryLiveCollector
    {
        public PageLifecycleTelemetryCollector(ITelemetryPage page, TelemetryBuffer buffer, ITelemetryClock clock)
        {
            _page = page;
            _buffer = buffer;
            _clock = clock;
        }

        // A tab switched back and forth all the time stops marking: its first events are the ones that tell.
        public const int MaxMarks = 64;

        private readonly ITelemetryPage _page;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private int _marks;

        public void Start(IReadOnlyLifetime running)
        {
            _marks = 0;
            _page.Lifecycle.Advise(running, OnEvent);
        }

        private void OnEvent(TelemetryPageEvent kind)
        {
            if (_marks == MaxMarks)
                return;

            _marks++;
            _buffer.AddMark(_clock.ElapsedMilliseconds, NameOf(kind));
        }

        private static string NameOf(TelemetryPageEvent kind)
        {
            return kind switch
            {
                TelemetryPageEvent.Hidden => "page.hidden",
                TelemetryPageEvent.Visible => "page.visible",
                TelemetryPageEvent.PageHide => "page.pagehide",
                TelemetryPageEvent.PageHidePersisted => "page.pagehide.persisted",
                TelemetryPageEvent.PageShowPersisted => "page.pageshow.persisted",
                TelemetryPageEvent.Freeze => "page.freeze",
                TelemetryPageEvent.Resume => "page.resume",
                TelemetryPageEvent.FullscreenEnter => "page.fullscreen.enter",
                TelemetryPageEvent.FullscreenExit => "page.fullscreen.exit",
                TelemetryPageEvent.AudioRunning => "audio.context.running",
                TelemetryPageEvent.AudioSuspended => "audio.context.suspended",
                TelemetryPageEvent.AudioInterrupted => "audio.context.interrupted",
                TelemetryPageEvent.AudioClosed => "audio.context.closed",
                TelemetryPageEvent.AudioOther => "audio.context.other",
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "[Telemetry] Unknown event of the page")
            };
        }
    }
}
