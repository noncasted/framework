using System;

namespace Global.Telemetry
{
    // The trace as the store keeps it, read by JsonUtility only: the names of the fields are the format. -1: no sample yet, no
    // duration of a mark; an empty detail is none (JsonUtility writes a null text as empty).
    [Serializable]
    public class TelemetrySessionTraceData
    {
        public string id;
        public string[] tags;
        public long lastSample = -1;
        public long writtenAt;
        public long[] markTimes;
        public string[] markNames;
        public string[] markDetails;
        public long[] markDurations;
    }
}
