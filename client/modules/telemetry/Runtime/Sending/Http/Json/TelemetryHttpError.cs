using System;

namespace Global.Telemetry
{
    // level by name: Error, Exception, Warning. category: "" for none (JsonUtility writes a null string so).
    [Serializable]
    public class TelemetryHttpError
    {
        public long time;
        public string level;
        public string message;
        public string stack;
        public int repeat;
        public string category;
    }
}
