namespace Global.Telemetry
{
    // The life of the browser page, as TelemetryPage.jslib numbers it: the numbers are the format between the two.
    public enum TelemetryPageEvent
    {
        Hidden = 0,
        Visible = 1,

        // Going away: a reload, a navigation, a closed tab. Persisted: into the back-forward cache, it may come back.
        PageHide = 2,
        PageHidePersisted = 3,

        // Back from the back-forward cache.
        PageShowPersisted = 4,

        // The browser stopped the tasks of a page in the background (Chromium) and started them again.
        Freeze = 5,
        Resume = 6,

        FullscreenEnter = 7,
        FullscreenExit = 8,

        // The AudioContext of Unity changed its state. Interrupted: Safari took the audio away (a call, another tab).
        AudioRunning = 9,
        AudioSuspended = 10,
        AudioInterrupted = 11,
        AudioClosed = 12,
        AudioOther = 13
    }
}
