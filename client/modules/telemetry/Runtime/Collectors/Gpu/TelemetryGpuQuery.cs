#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryGpuQuery.jslib.
    public class TelemetryGpuQuery : ITelemetryGpuQuery
    {
        public bool IsAvailable => TelemetryGpuQueryIsAvailable();

        public bool Begin()
        {
            return TelemetryGpuQueryBegin();
        }

        public void End()
        {
            TelemetryGpuQueryEnd();
        }

        public double TakeMs()
        {
            return TelemetryGpuQueryTake();
        }

        public void Drop()
        {
            TelemetryGpuQueryDrop();
        }

        [DllImport("__Internal")]
        private static extern bool TelemetryGpuQueryIsAvailable();

        [DllImport("__Internal")]
        private static extern bool TelemetryGpuQueryBegin();

        [DllImport("__Internal")]
        private static extern void TelemetryGpuQueryEnd();

        [DllImport("__Internal")]
        private static extern double TelemetryGpuQueryTake();

        [DllImport("__Internal")]
        private static extern void TelemetryGpuQueryDrop();
    }
}
#endif
