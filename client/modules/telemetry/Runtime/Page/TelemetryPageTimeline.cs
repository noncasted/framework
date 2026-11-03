using System;

namespace Global.Telemetry
{
    // The page before the build, as TelemetryPageTimeline.jslib reads it: every time in milliseconds of performance.now(), from
    // performance.timeOrigin. Fields for JsonUtility.
    [Serializable]
    public class TelemetryPageTimeline
    {
        // performance.now() at the moment of the reading.
        public double now;
        public TelemetryPageNavigation navigation = new();
        public TelemetryPageFile[] files = Array.Empty<TelemetryPageFile>();
        public TelemetryPageScriptSpan[] spans = Array.Empty<TelemetryPageScriptSpan>();
    }
}
