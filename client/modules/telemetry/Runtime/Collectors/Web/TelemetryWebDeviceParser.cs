using UnityEngine;

namespace Global.Telemetry
{
    public static class TelemetryWebDeviceParser
    {
        public static TelemetryDevice Parse(string json, string buildVersion)
        {
            var data = JsonUtility.FromJson<TelemetryWebDeviceData>(json);

            return new TelemetryDevice(
                platform: "webgl",
                buildVersion: buildVersion,
                browser: Text(data.browser),
                browserMajor: Number(data.browserMajor),
                os: Text(data.os),
                osMajor: Number(data.osMajor),
                gpu: Text(data.gpu),
                cores: Number(data.cores),
                memoryClass: data.memoryClass > 0d ? data.memoryClass : null,
                screenWidth: Number(data.screenWidth),
                screenHeight: Number(data.screenHeight),
                dpr: data.dpr > 0d ? data.dpr : null);
        }

        private static string Text(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static int? Number(int number)
        {
            return number >= 0 ? number : null;
        }
    }
}
