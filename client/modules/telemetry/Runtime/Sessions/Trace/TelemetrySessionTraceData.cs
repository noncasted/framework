using System;

namespace Global.Telemetry
{
    // The trace as the store keeps it, read by JsonUtility only: the names of the fields are the format. -1: no sample yet.
    [Serializable]
    public class TelemetrySessionTraceData
    {
        public string id;
        public string[] tags;
        public long lastSample = -1;
        public long writtenAt;
        public long[] markTimes;
        public string[] markNames;
    }
}
