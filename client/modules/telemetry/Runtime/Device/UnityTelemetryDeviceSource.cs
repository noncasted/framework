using System;
using UnityEngine;

namespace Global.Telemetry
{
    // A player outside the web: what Unity knows without a browser API. SystemInfo.deviceUniqueIdentifier and deviceName are personal data and must
    // never be read here; the rest of SystemInfo is added only under a field of the closed list of TelemetryDevice.
    public class UnityTelemetryDeviceSource : ITelemetryDeviceSource
    {
        private const double _minMemoryPower = -2;
        private const double _maxMemoryPower = 10;
        private const double _megabytesInGigabyte = 1024;
        private const double _androidBaseDpi = 160;
        private const double _iosBaseDpi = 163;

        public TelemetryDevice Read()
        {
            var os = TelemetryProductVersion.Parse(SystemInfo.operatingSystem);

            return new TelemetryDevice(
                platform: PlatformName(Application.platform),
                buildVersion: Application.version,
                os: os.Name,
                osMajor: os.Major,
                gpu: SystemInfo.graphicsDeviceName,
                cores: SystemInfo.processorCount,
                memoryClass: MemoryClass(SystemInfo.systemMemorySize),
                screenWidth: Screen.width,
                screenHeight: Screen.height,
                dpr: Dpr(Application.platform, Screen.dpi));
        }

        // The nearest power of two in GB, like navigator.deviceMemory: a class, not the exact size of this device.
        public static double? MemoryClass(int megabytes)
        {
            if (megabytes <= 0)
                return null;

            var power = Math.Round(Math.Log(megabytes / _megabytesInGigabyte, 2));

            return Math.Pow(2, Math.Clamp(power, _minMemoryPower, _maxMemoryPower));
        }

        // Only where the base of the dpi is a rule of the platform: elsewhere the number would be a guess. Zero is no dpi.
        public static double? Dpr(RuntimePlatform platform, float dpi)
        {
            if (dpi <= 0f)
                return null;

            return platform switch
            {
                RuntimePlatform.Android => dpi / _androidBaseDpi,
                RuntimePlatform.IPhonePlayer => dpi / _iosBaseDpi,
                _ => null
            };
        }

        private static string PlatformName(RuntimePlatform platform)
        {
            return platform switch
            {
                RuntimePlatform.WebGLPlayer => "webgl",
                RuntimePlatform.Android => "android",
                RuntimePlatform.IPhonePlayer => "ios",
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => "windows",
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => "macos",
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => "linux",
                _ => platform.ToString().ToLowerInvariant()
            };
        }
    }
}
