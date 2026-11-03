namespace Global.Telemetry
{
    // The closed list of what a client tells about its device: only what explains the performance, nothing about the
    // player. No user agent, no ids, no names. Null is not known, not sent.
    public class TelemetryDevice
    {
        public TelemetryDevice(
            string platform = null,
            string buildVersion = null,
            string browser = null,
            int? browserMajor = null,
            string os = null,
            int? osMajor = null,
            string gpu = null,
            int? cores = null,
            double? memoryClass = null,
            int? screenWidth = null,
            int? screenHeight = null,
            double? dpr = null)
        {
            Platform = platform;
            BuildVersion = buildVersion;
            Browser = browser;
            BrowserMajor = browserMajor;
            Os = os;
            OsMajor = osMajor;
            Gpu = gpu;
            Cores = cores;
            MemoryClass = memoryClass;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            Dpr = dpr;
        }

        public string Platform { get; }
        public string BuildVersion { get; }
        public string Browser { get; }
        public int? BrowserMajor { get; }
        public string Os { get; }
        public int? OsMajor { get; }
        public string Gpu { get; }
        public int? Cores { get; }
        public double? MemoryClass { get; }
        public int? ScreenWidth { get; }
        public int? ScreenHeight { get; }
        public double? Dpr { get; }
    }
}
