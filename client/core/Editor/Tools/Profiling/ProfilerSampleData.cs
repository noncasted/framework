using System;

namespace Internal
{
    [Serializable]
    public class ProfilerSampleData
    {
        public int Depth;
        public string Name;
        public double TotalMs;
        public double SelfMs;
        public int Calls;
        public long GcAlloc;
    }
}
