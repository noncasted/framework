#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace Global.Telemetry
{
    // Navigation Timing, Resource Timing and the spans of window.startupTrace (TelemetryPageTimeline.jslib).
    public class WebTelemetryPageTimeline : ITelemetryPageTimeline
    {
        public TelemetryPageTimeline Read()
        {
            return JsonUtility.FromJson<TelemetryPageTimeline>(TelemetryPageTimelineRead());
        }

        [DllImport("__Internal")]
        private static extern string TelemetryPageTimelineRead();
    }
}
#endif
