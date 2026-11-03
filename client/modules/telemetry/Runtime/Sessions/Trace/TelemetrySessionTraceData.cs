using System;

namespace Global.Telemetry
{
    // The trace as the store keeps it, read by JsonUtility only: the names of the fields are the format. -1: no sample yet, no
    // duration of a mark, no value of a sample; an empty detail is none (JsonUtility writes a null text as empty).
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

        // The samples, oldest first: sampleValues holds sampleNames.Length values per sample, -1 for none (every traced number is a
        // time or a count). A trace written before them has none of the arrays.
        public string[] sampleNames;
        public long[] sampleTimes;
        public bool[] sampleHidden;
        public double[] sampleValues;
    }
}
