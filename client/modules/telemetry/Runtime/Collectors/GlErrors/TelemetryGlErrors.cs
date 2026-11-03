#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryGlErrors.jslib.
    public class TelemetryGlErrors : ITelemetryGlErrors
    {
        public int Take(int[] into)
        {
            return TelemetryGlErrorsTake(into, into.Length);
        }

        public double LastTakeMs => TelemetryGlErrorsLastMs();

        [DllImport("__Internal")]
        private static extern int TelemetryGlErrorsTake(int[] into, int max);

        [DllImport("__Internal")]
        private static extern double TelemetryGlErrorsLastMs();
    }
}
#endif
