using System;

namespace Measurement
{
    // What the editor knows of the build; the sizes of the files are measured by tools/measure.py.
    [Serializable]
    public sealed class MeasurementBuildRecord
    {
        public string unityVersion;
        public string result;
        public double buildSeconds;
        public long reportedSize;
        public int errors;
        public int warnings;
        public int strippedSubmodules;
        public string missingSubmodules;
        public bool profiled;
    }
}
