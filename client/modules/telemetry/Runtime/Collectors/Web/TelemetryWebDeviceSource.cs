#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace Global.Telemetry
{
    // The user agent is parsed on the page: the full one never gets here, see TelemetryWeb.jslib.
    public class TelemetryWebDeviceSource : ITelemetryDeviceSource
    {
        public TelemetryDevice Read()
        {
            return TelemetryWebDeviceParser.Parse(TelemetryWebReadDevice(), Application.version);
        }

        [DllImport("__Internal")]
        private static extern string TelemetryWebReadDevice();
    }
}
#endif
