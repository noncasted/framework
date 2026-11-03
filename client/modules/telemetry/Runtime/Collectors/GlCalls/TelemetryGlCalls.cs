#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryGlCalls.jslib: the page counts from the start of framework.js, the build of the scope gives the threshold of a slow call.
    public class TelemetryGlCalls : ITelemetryGlCalls
    {
        public TelemetryGlCalls(ClientTelemetryOptions options)
        {
            TelemetryGlCallsInstall(options.SlowShaderMs);
        }

        public int Take(double[] into)
        {
            return TelemetryGlCallsTake(into, TelemetryGlCallsLayout.MaxSlowPerTake);
        }

        [DllImport("__Internal")]
        private static extern void TelemetryGlCallsInstall(double slowMs);

        [DllImport("__Internal")]
        private static extern int TelemetryGlCallsTake(double[] into, int maxSlow);
    }
}
#endif
