#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryRaf.jslib.
    public class TelemetryRaf : ITelemetryRaf
    {
        public void Start()
        {
            TelemetryRafStart();
        }

        public void Stop()
        {
            TelemetryRafStop();
        }

        public double TakeAverageMs()
        {
            return TelemetryRafTakeAverage();
        }

        public double TakeMaxMs()
        {
            return TelemetryRafTakeMax();
        }

        [DllImport("__Internal")]
        private static extern void TelemetryRafStart();

        [DllImport("__Internal")]
        private static extern void TelemetryRafStop();

        [DllImport("__Internal")]
        private static extern double TelemetryRafTakeAverage();

        [DllImport("__Internal")]
        private static extern double TelemetryRafTakeMax();
    }
}
#endif
