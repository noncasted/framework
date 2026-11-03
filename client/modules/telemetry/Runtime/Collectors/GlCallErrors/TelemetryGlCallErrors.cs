#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Global.Telemetry
{
    // TelemetryGlCallErrors.jslib.
    public class TelemetryGlCallErrors : ITelemetryGlCallErrors
    {
        private readonly int[] _counts = new int[3];

        public TelemetryGlCallErrorCounts Take(List<TelemetryGlCallError> details)
        {
            if (TelemetryGlCallErrorsTake(_counts) > 0)
                TelemetryGlCallErrorLines.Parse(TelemetryGlCallErrorsReadDetails(), details);

            return new TelemetryGlCallErrorCounts(_counts[0], _counts[1], _counts[2] != 0);
        }

        [DllImport("__Internal")]
        private static extern int TelemetryGlCallErrorsTake(int[] into);

        [DllImport("__Internal")]
        private static extern string TelemetryGlCallErrorsReadDetails();
    }
}
#endif
