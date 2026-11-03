using System;

namespace Global.Telemetry
{
    // What TelemetryWeb.jslib hands over, read by JsonUtility only: the names of the fields are the format. Empty text and
    // -1 are not known.
    [Serializable]
    public class TelemetryWebDeviceData
    {
        public string browser;
        public int browserMajor;
        public string os;
        public int osMajor;
        public string gpu;
        public int cores;
        public double memoryClass;
        public int screenWidth;
        public int screenHeight;
        public double dpr;
    }
}
